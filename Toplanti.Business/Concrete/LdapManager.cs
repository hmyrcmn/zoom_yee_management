using Microsoft.Extensions.Configuration;
using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Toplanti.Business.Abstract;
using Toplanti.Core.Entities.Concrete;
using Toplanti.Entities.DTOs;

namespace Toplanti.Business.Concrete
{
    public class LdapManager : ILdapService
    {
        private readonly LdapSettings _ldapSettings;

        public LdapManager(IConfiguration configuration)
        {
            _ldapSettings = configuration.GetSection("LdapSettings").Get<LdapSettings>() ?? new LdapSettings();
        }

        public LdapUser? ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("[LDAP Warning] Empty username or password.");
                return null;
            }

            using (var connection = new LdapConnection())
            {
                try
                {
                    string host = _ldapSettings.Host ?? "172.17.60.20";
                    int port = _ldapSettings.Port;
                    string domain = _ldapSettings.Domain ?? "yee.org.tr";
                    string baseDn = _ldapSettings.BaseDn ?? "DC=yee,DC=org,DC=tr";

                    Console.WriteLine($"[LDAP Info] Connect host={host}, port={port}, baseDn={baseDn}, inputUser={username}");

                    connection.SecureSocketLayer = false;
                    connection.ConnectionTimeout = 10000;
                    connection.Connect(host, port);

                    if (!connection.Connected)
                    {
                        Console.WriteLine($"[LDAP Error] Failed to connect to {host}:{port}");
                        return null;
                    }

                    string rawUsername = username.Contains("@") ? username.Split('@')[0] : username;
                    string userUpn = username.Contains("@") ? username : $"{rawUsername}@{domain}";
                    string searchFilter = $"(&(objectClass=user)(sAMAccountName={rawUsername}))";
                    Console.WriteLine($"[LDAP Search] baseDn={baseDn}, finalFilter={searchFilter}");
                    var bindCandidates = new List<(string Method, string BindIdentity)>
                    {
                        ("UPN", userUpn),
                        ("sAMAccountName", rawUsername)
                    };

                    string? boundIdentity = null;
                    foreach (var candidate in bindCandidates.Where(c => !string.IsNullOrWhiteSpace(c.BindIdentity))
                                                           .DistinctBy(c => c.BindIdentity, StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            Console.WriteLine($"[LDAP Bind Attempt] method={candidate.Method}, User DN={candidate.BindIdentity}");
                            connection.Bind(candidate.BindIdentity, password);
                            if (connection.Bound)
                            {
                                boundIdentity = candidate.BindIdentity;
                                Console.WriteLine($"[LDAP Bind Success] method={candidate.Method}, userDn={candidate.BindIdentity}");
                                break;
                            }
                        }
                        catch (LdapException lex)
                        {
                            var ldapSpecificErrorCode = TryExtractDirectoryErrorCode(lex.Message);
                            Console.WriteLine(
                                $"[LDAP Bind Failed] method={candidate.Method}, userDn={candidate.BindIdentity}, resultCode={(int)lex.ResultCode}({lex.ResultCode}), ldapErrorCode={ldapSpecificErrorCode ?? "N/A"}, message={lex.Message}");
                        }
                    }

                    if (!connection.Bound)
                    {
                        Console.WriteLine($"[LDAP Error] Bind failed for user={username}. Tried identities: {string.Join(", ", bindCandidates.Select(c => $"{c.Method}:{c.BindIdentity}"))}");
                        return null;
                    }

                    var searchResults = connection.Search(
                        baseDn,
                        LdapConnection.ScopeSub,
                        searchFilter,
                        new[] { "givenName", "sn", "mail", "memberOf", "department" },
                        false
                    );

                    LdapUser ldapUser = new LdapUser 
                    { 
                        Username = rawUsername, 
                        Email = $"{rawUsername}@{domain}" 
                    };

                    if (searchResults.HasMore())
                    {
                        var entry = searchResults.Next();
                        Console.WriteLine($"[LDAP Info] User details resolved for sAMAccountName={rawUsername}, ldapDn={entry.Dn}, bindUserDn={boundIdentity}");
                        
                        ldapUser.Name = SafeGetAttribute(entry, "givenName") ?? rawUsername;
                        ldapUser.Surname = SafeGetAttribute(entry, "sn") ?? "";
                        ldapUser.Email = SafeGetAttribute(entry, "mail") ?? ldapUser.Email;
                        ldapUser.Department = SafeGetAttribute(entry, "department") ?? string.Empty;

                        var memberOfHeader = entry.GetAttribute("memberOf");
                        if (memberOfHeader != null)
                        {
                            foreach (var groupDn in memberOfHeader.StringValueArray)
                            {
                                string groupName = ParseGroupName(groupDn);
                                if (!string.IsNullOrEmpty(groupName))
                                {
                                    ldapUser.Groups.Add(groupName);
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[LDAP Warning] No details found for bound user: {rawUsername}");
                    }

                    return ldapUser;
                }
                catch (LdapException lex)
                {
                    var ldapSpecificErrorCode = TryExtractDirectoryErrorCode(lex.Message);
                    Console.WriteLine($"[LDAP Error] LdapException: {lex.Message}, ResultCode: {(int)lex.ResultCode}({lex.ResultCode}), ldapErrorCode={ldapSpecificErrorCode ?? "N/A"}");
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LDAP Error] General Exception: {ex.Message}");
                    return null;
                }
            }
        }

        private static string? TryExtractDirectoryErrorCode(string? ldapMessage)
        {
            if (string.IsNullOrWhiteSpace(ldapMessage))
            {
                return null;
            }

            // Active Directory often returns a diagnostic code like "data 52e".
            var match = Regex.Match(ldapMessage, @"data\s+([0-9a-fA-F]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private string? SafeGetAttribute(LdapEntry entry, string attrName)
        {
            try 
            {
                // Novell.Directory.Ldap GetAttribute can throw if column doesn't exist in some versions/configs
                var attr = entry.GetAttribute(attrName);
                return attr?.StringValue;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string ParseGroupName(string dn)
        {
            try
            {
                var parts = dn.Split(',');
                foreach (var part in parts)
                {
                    if (part.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    {
                        return part.Split('=')[1];
                    }
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
