using Toplanti.Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.DTOs;
using Toplanti.DataAccess.Abstract;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.Core.Entities.Concrete;

namespace Toplanti.Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private ISsoApi _ssoApi;
        private ILdapService _ldapService;
        private IUserDal _userDal;
        private ITokenHelper _tokenHelper;
        private IOperationClaimDal _operationClaimDal;
        private IUserOperationClaimDal _userOperationClaimDal;

        public AuthManager(
            ISsoApi ssoApi, 
            ILdapService ldapService, 
            IUserDal userDal, 
            ITokenHelper tokenHelper,
            IOperationClaimDal operationClaimDal,
            IUserOperationClaimDal userOperationClaimDal)
        {
            _ssoApi = ssoApi;
            _ldapService = ldapService;
            _userDal = userDal;
            _tokenHelper = tokenHelper;
            _operationClaimDal = operationClaimDal;
            _userOperationClaimDal = userOperationClaimDal;
        }

        public IDataResult<UserStudentDto> UserInfo()
        {
            var userSsoId = new UserCookie().UserId();
            var userinfo = _ssoApi.GetSsoUserInfoId(userSsoId);
            return new SuccessDataResult<UserStudentDto>(userinfo, "Kullanıcı Bilgileri");
        }

        public IDataResult<AccessToken> Login(UserForLoginDto userForLoginDto)
        {
            // 1. LDAP Validation & Detail Retrieval
            var ldapUser = _ldapService.ValidateUser(userForLoginDto.Email, userForLoginDto.Password);
            if (ldapUser == null)
            {
                return new ErrorDataResult<AccessToken>("Kullanıcı adı veya şifre hatalı");
            }

            // 2. Sync Logic (Check Local DB)
            var userToCheck = _userDal.Get(u => u.Email == ldapUser.Email);

            if (userToCheck == null)
            {
                // Create New User
                var newUser = new Toplanti.Core.Entities.Concrete.User
                {
                    Username = ldapUser.Username ?? string.Empty,
                    FirstName = ldapUser.Name ?? string.Empty,
                    LastName = ldapUser.Surname ?? string.Empty,
                    Email = ldapUser.Email ?? string.Empty,
                    Status = true,
                    AddedTime = DateTime.Now,
                    Active = true,
                    Deleted = false,
                    PasswordHash = new byte[0], 
                    PasswordSalt = new byte[0]
                };
                _userDal.Add(newUser);
                userToCheck = _userDal.Get(u => u.Email == ldapUser.Email);
            }
            else
            {
                // Update Existing User
                userToCheck.FirstName = ldapUser.Name ?? userToCheck.FirstName;
                userToCheck.LastName = ldapUser.Surname ?? userToCheck.LastName;
                _userDal.Update(userToCheck);
            }

            // 3. Sync Claims from LDAP Groups
            SyncUserClaims(userToCheck!.Id, ldapUser.Groups);

            // 4. Generate Token
            var userClaims = GetClaims(userToCheck.Id);
            var accessToken = _tokenHelper.CreateToken(userToCheck, userClaims);
            
            return new SuccessDataResult<AccessToken>(accessToken, "Giriş başarılı");
        }

        private void SyncUserClaims(int userId, List<string> ldapGroups)
        {
            // Define Mapping Rules
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MeetingAdmins", "Admin" },
                { "StandardUsers", "User" },
                { "ToplantiYonetici", "Admin" },
                { "ToplantiKullanici", "User" }
            };

            var targetClaimNames = ldapGroups
                .Select(g => mapping.TryGetValue(g, out var cn) ? cn : null)
                .Where(cn => cn != null)
                .ToList();

            // Fallback default
            if (!targetClaimNames.Any())
            {
                targetClaimNames.Add("User");
            }

            targetClaimNames = targetClaimNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Fetch all system claims
            var allSystemClaims = _operationClaimDal.GetAll(c => c.Active && !c.Deleted);
            
            // Get user's current claims (all, to manage duplicates)
            var existingUserClaims = _userOperationClaimDal.GetAll(u => u.UserId == userId);

            // 1. Resolve Target Claim IDs
            var targetClaimIds = new List<int>();
            foreach (var claimName in targetClaimNames)
            {
                var claim = allSystemClaims.FirstOrDefault(c => c.Name.Equals(claimName, StringComparison.OrdinalIgnoreCase));
                if (claim == null)
                {
                    claim = new OperationClaim { Name = claimName };
                    _operationClaimDal.Add(claim);
                    allSystemClaims = _operationClaimDal.GetAll(c => c.Active && !c.Deleted);
                    claim = allSystemClaims.FirstOrDefault(c => c.Name.Equals(claimName, StringComparison.OrdinalIgnoreCase));
                }
                if (claim != null) targetClaimIds.Add(claim.Id);
            }

            // 2. Synchronize each System Claim
            foreach (var systemClaim in allSystemClaims)
            {
                var currentRelations = existingUserClaims.Where(uc => uc.OperationClaimId == systemClaim.Id).ToList();
                bool shouldHave = targetClaimIds.Contains(systemClaim.Id);

                if (shouldHave)
                {
                    if (!currentRelations.Any())
                    {
                        // Add missing
                        _userOperationClaimDal.Add(new UserOperationClaim
                        {
                            UserId = userId,
                            OperationClaimId = systemClaim.Id,
                            Active = true,
                            Deleted = false
                        });
                    }
                    else
                    {
                        // Manage duplicates: Keep only the first one active
                        bool skipFirst = false;
                        foreach (var rel in currentRelations)
                        {
                            if (!skipFirst)
                            {
                                if (!rel.Active || rel.Deleted)
                                {
                                    rel.Active = true;
                                    rel.Deleted = false;
                                    _userOperationClaimDal.Update(rel);
                                }
                                skipFirst = true;
                            }
                            else if (rel.Active || !rel.Deleted)
                            {
                                // Deactivate duplicates
                                _userOperationClaimDal.Delete(rel);
                            }
                        }
                    }
                }
                else
                {
                    // Deactivate all matching relations
                    foreach (var rel in currentRelations)
                    {
                        if (rel.Active || !rel.Deleted)
                        {
                            _userOperationClaimDal.Delete(rel);
                        }
                    }
                }
            }
        }

        private List<OperationClaim> GetClaims(int userId)
        {
            var userClaims = _userOperationClaimDal.GetAll(u => u.UserId == userId && u.Active && !u.Deleted);
            var systemClaims = _operationClaimDal.GetAll(c => c.Active && !c.Deleted);

            return systemClaims.Where(sc => userClaims.Any(uc => uc.OperationClaimId == sc.Id)).ToList();
        }
    }
}
