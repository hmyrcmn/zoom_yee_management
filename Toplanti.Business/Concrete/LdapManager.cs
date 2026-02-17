using Microsoft.Extensions.Configuration;
using Novell.Directory.Ldap;
using System;
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

        public LdapUser? GetUserDetails(string username)
        {
            try
            {
                using (var connection = new LdapConnection())
                {
                    string host = _ldapSettings.Host ?? "localhost";
                    int port = _ldapSettings.Port;
                    string domain = _ldapSettings.Domain ?? "example.com";

                    Console.WriteLine($"[LDAP] Connecting to {host}:{port} for user details: {username} (SSL: false, Timeout: 10s)");
                    connection.SecureSocketLayer = false;
                    connection.ConnectionTimeout = 10000;
                    connection.Connect(host, port);
                    Console.WriteLine("SUCCESS: Connected to LDAP for user details retrieval.");
                    
                    return new LdapUser
                    {
                        Username = username,
                        Email = $"{username}@{domain}",
                        Name = username,
                        Surname = ""
                    };
                }
            }
            catch (LdapException ex)
            {
                Console.WriteLine($"[LDAP Error] GetUserDetails failed. Message: {ex.Message}, ResultCode: {ex.ResultCode}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] GetUserDetails failed: {ex.Message}");
                return null;
            }
        }

        public bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("[LDAP] ValidateUser failed: Username or password empty.");
                return false;
            }

            using (var connection = new LdapConnection())
            {
                try
                {
                    string host = _ldapSettings.Host ?? "localhost";
                    int port = _ldapSettings.Port;
                    string domain = _ldapSettings.Domain ?? "example.com";
                    
                    Console.WriteLine($"[LDAP] Attempting to connect to {host}:{port} (SSL: false, Timeout: 10s)");
                    connection.SecureSocketLayer = false;
                    connection.ConnectionTimeout = 10000;
                    connection.Connect(host, port);
                    Console.WriteLine("SUCCESS: Connected to LDAP for validation.");
                    
                    if (!connection.Connected)
                    {
                        Console.WriteLine("[LDAP Error] Connection object says it's not connected.");
                        return false;
                    }

                    string userUpn = username.Contains("@") ? username : $"{username}@{domain}";
                    Console.WriteLine($"[LDAP] Attempting Bind with UPN: {userUpn}");
                    
                    try 
                    {
                        connection.Bind(userUpn, password);
                        if (connection.Bound)
                        {
                            Console.WriteLine($"[LDAP Success] User authenticated successfully with UPN: {userUpn}");
                            return true;
                        }
                    }
                    catch (LdapException ex) when (ex.ResultCode == 49)
                    {
                        Console.WriteLine($"[LDAP Info] UPN Bind failed (Invalid Credentials). Trying raw username: {username}");
                    }

                    if (!username.Contains("@"))
                    {
                        Console.WriteLine($"[LDAP] Attempting Bind with raw username: {username}");
                        connection.Bind(username, password);
                        if (connection.Bound)
                        {
                            Console.WriteLine($"[LDAP Success] User authenticated successfully with raw username: {username}");
                            return true;
                        }
                    }

                    Console.WriteLine("[LDAP Failure] All bind attempts failed.");
                    return false;
                }
                catch (LdapException ex)
                {
                    Console.WriteLine($"[LDAP Error] Bind failed for user {username}.");
                    Console.WriteLine("Message: " + ex.Message);
                    Console.WriteLine("Result Code: " + ex.ResultCode);

                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[General Error] LDAP Validation failed: " + ex.Message);
                    return false;
                }
            }
        }
    }
}
