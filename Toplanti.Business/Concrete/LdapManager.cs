using Microsoft.Extensions.Configuration;
using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Linq;
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

                    connection.SecureSocketLayer = false;
                    connection.ConnectionTimeout = 10000;
                    connection.Connect(host, port);

                    if (!connection.Connected)
                    {
                        Console.WriteLine($"[LDAP Error] Failed to connect to {host}:{port}");
                        return null;
                    }

                    string userUpn = username.Contains("@") ? username : $"{username}@{domain}";
                    
                    try
                    {
                        connection.Bind(userUpn, password);
                    }
                    catch (LdapException lex) when (lex.ResultCode == 49 && !username.Contains("@"))
                    {
                        connection.Bind(username, password);
                    }

                    if (!connection.Bound)
                    {
                        Console.WriteLine($"[LDAP Error] Bind failed for user: {username}");
                        return null;
                    }

                    string rawUsername = username.Contains("@") ? username.Split('@')[0] : username;
                    string searchFilter = $"(&(objectClass=user)(sAMAccountName={rawUsername}))";
                    
                    var searchResults = connection.Search(
                        baseDn,
                        LdapConnection.ScopeSub,
                        searchFilter,
                        new[] { "givenName", "sn", "mail", "memberOf" },
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
                        
                        ldapUser.Name = SafeGetAttribute(entry, "givenName") ?? rawUsername;
                        ldapUser.Surname = SafeGetAttribute(entry, "sn") ?? "";
                        ldapUser.Email = SafeGetAttribute(entry, "mail") ?? ldapUser.Email;

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
                    Console.WriteLine($"[LDAP Error] LdapException: {lex.Message}, ResultCode: {lex.ResultCode}");
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LDAP Error] General Exception: {ex.Message}");
                    return null;
                }
            }
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
