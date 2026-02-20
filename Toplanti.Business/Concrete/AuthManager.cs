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
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Toplanti.Core.Extensisons;
using Toplanti.Core.Utilities.Security.Hashing;
using System.Globalization;

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
        private IZoomService _zoomService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthManager(
            ISsoApi ssoApi, 
            ILdapService ldapService, 
            IUserDal userDal, 
            ITokenHelper tokenHelper,
            IOperationClaimDal operationClaimDal,
            IUserOperationClaimDal userOperationClaimDal,
            IZoomService zoomService,
            IHttpContextAccessor httpContextAccessor)
        {
            _ssoApi = ssoApi;
            _ldapService = ldapService;
            _userDal = userDal;
            _tokenHelper = tokenHelper;
            _operationClaimDal = operationClaimDal;
            _userOperationClaimDal = userOperationClaimDal;
            _zoomService = zoomService;
            _httpContextAccessor = httpContextAccessor;
        }

        public IDataResult<UserStudentDto> UserInfo()
        {
            try
            {
                var userPrincipal = _httpContextAccessor.HttpContext?.User;
                var rawUserId = userPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(rawUserId))
                {
                    return new ErrorDataResult<UserStudentDto>("Kullanıcı kimliği doğrulanamadı.");
                }

                var userSsoId = ResolveLegacySsoUserId(rawUserId);
                if (userSsoId > 0)
                {
                    try
                    {
                        var ssoUserInfo = _ssoApi.GetSsoUserInfoId(userSsoId);
                        if (ssoUserInfo != null)
                        {
                            ssoUserInfo.Department = userPrincipal?.Department() ?? string.Empty;
                            return new SuccessDataResult<UserStudentDto>(ssoUserInfo, "Kullanıcı Bilgileri");
                        }
                    }
                    catch
                    {
                        // SSO may be temporarily unavailable; fallback to local identity data below.
                    }
                }

                var localUserInfo = BuildLocalUserInfo(rawUserId, userPrincipal);
                if (localUserInfo == null)
                {
                    return new ErrorDataResult<UserStudentDto>("Kullanıcı bilgileri getirilemedi.");
                }

                return new SuccessDataResult<UserStudentDto>(localUserInfo, "Kullanıcı Bilgileri (Yerel)");
            }
            catch (Exception ex)
            {
                return new ErrorDataResult<UserStudentDto>($"Kullanıcı bilgileri alınırken hata oluştu: {ex.Message}");
            }
        }

        private int ResolveLegacySsoUserId(string rawUserId)
        {
            if (int.TryParse(rawUserId, out var parsedId))
            {
                return parsedId;
            }

            // Legacy fallback: extract numeric ID from mixed claim values (e.g. "user-123").
            var numericPart = Regex.Replace(rawUserId, "[^0-9]", string.Empty);
            if (int.TryParse(numericPart, out parsedId))
            {
                return parsedId;
            }

            return 0;
        }

        private UserStudentDto BuildLocalUserInfo(string rawUserId, ClaimsPrincipal userPrincipal)
        {
            int.TryParse(rawUserId, out var localUserId);
            var emailFromClaim = userPrincipal?.FindFirst(ClaimTypes.Email)?.Value;
            var fullNameFromClaim = userPrincipal?.FindFirst(ClaimTypes.Name)?.Value;

            var localUser = localUserId > 0
                ? _userDal.Get(u => u.Id == localUserId && u.Active && !u.Deleted)
                : null;

            if (localUser == null && !string.IsNullOrWhiteSpace(emailFromClaim))
            {
                localUser = _userDal.Get(u => u.Email == emailFromClaim && u.Active && !u.Deleted);
            }

            if (localUser == null && string.IsNullOrWhiteSpace(emailFromClaim) && string.IsNullOrWhiteSpace(fullNameFromClaim))
            {
                return null;
            }

            var fallback = new UserStudentDto
            {
                FirstName = localUser?.FirstName ?? ExtractFirstName(fullNameFromClaim),
                LastName = localUser?.LastName ?? ExtractLastName(fullNameFromClaim),
                Email = localUser?.Email ?? emailFromClaim ?? string.Empty,
                Department = userPrincipal?.Department() ?? string.Empty,
                ImagePath = string.Empty,
                ClassName = string.Empty,
                ProfileImage = string.Empty
            };

            return fallback;
        }

        private string ExtractFirstName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return string.Empty;
            }

            return fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }

        private string ExtractLastName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return string.Empty;
            }

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;
        }

        public IDataResult<AccessToken> Login(UserForLoginDto userForLoginDto)
        {
            // 1. LDAP Validation & Detail Retrieval
            var ldapUser = _ldapService.ValidateUser(userForLoginDto.Email, userForLoginDto.Password);
            if (ldapUser == null)
            {
                return new ErrorDataResult<AccessToken>("Kullanıcı adı veya şifre hatalı");
            }

            var isWebmaster = string.Equals(userForLoginDto.Email, "webmaster@yee.org.tr", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ldapUser.Email, "webmaster@yee.org.tr", StringComparison.OrdinalIgnoreCase);
            if (isWebmaster)
            {
                ldapUser.Department = "Bilişim";
            }

            var isActiveInZoom = _zoomService.IsUserActiveInZoom(ldapUser.Email).GetAwaiter().GetResult();
            if (!isActiveInZoom)
            {
                return new ErrorDataResult<AccessToken>("Zoom kaydınız aktifleşmemiş. Lütfen Bilişim birimi ile iletişime geçin.");
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

            // 2.1 Password verification must be explicit and strict.
            bool hasStoredPassword =
                userToCheck.PasswordHash != null && userToCheck.PasswordHash.Length > 0 &&
                userToCheck.PasswordSalt != null && userToCheck.PasswordSalt.Length > 0;

            string verifyFailureMessage;
            var isVerified = EnsurePasswordHashAndVerify(userToCheck, userForLoginDto.Password, hasStoredPassword, out verifyFailureMessage);
            if (!isVerified)
            {
                return new ErrorDataResult<AccessToken>(verifyFailureMessage);
            }

            // 3. Sync Claims from LDAP Groups
            SyncUserClaims(userToCheck!.Id, ldapUser.Groups, ldapUser.Department);

            // 4. Generate Token
            var userClaims = GetClaims(userToCheck.Id);
            var accessToken = _tokenHelper.CreateToken(userToCheck, userClaims, ldapUser.Department);
            
            return new SuccessDataResult<AccessToken>(accessToken, "Giriş başarılı");
        }

        private bool EnsurePasswordHashAndVerify(User user, string plainPassword, bool hasStoredPassword, out string failureMessage)
        {
            failureMessage = "Kullanıcı adı veya şifre hatalı";

            if (!hasStoredPassword)
            {
                try
                {
                    HashingHelper.CreatePasswordHash(plainPassword, out var passwordHash, out var passwordSalt);
                    user.PasswordHash = passwordHash;
                    user.PasswordSalt = passwordSalt;

                    // _userDal.Update internally calls SaveChanges (EfEntityRepositoryBase).
                    _userDal.Update(user);

                    var persistedUser = _userDal.Get(u => u.Id == user.Id);
                    var persistedHashAvailable = persistedUser?.PasswordHash != null && persistedUser.PasswordHash.Length > 0
                                                && persistedUser.PasswordSalt != null && persistedUser.PasswordSalt.Length > 0;
                    if (!persistedHashAvailable)
                    {
                        failureMessage = "Şifre bilgisi veritabanına kaydedilemedi.";
                        return false;
                    }

                    user.PasswordHash = persistedUser.PasswordHash;
                    user.PasswordSalt = persistedUser.PasswordSalt;
                    hasStoredPassword = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Password hash persistence failed for {user.Email}: {ex.Message}");
                    failureMessage = "Şifre bilgisi veritabanına kaydedilemedi.";
                    return false;
                }
            }

            var canVerify = hasStoredPassword
                            && user.PasswordHash != null
                            && user.PasswordHash.Length > 0
                            && user.PasswordSalt != null
                            && user.PasswordSalt.Length > 0;
            var isVerified = canVerify && HashingHelper.VerifyPasswordHash(plainPassword, user.PasswordHash, user.PasswordSalt);
            Console.WriteLine($"Password verify result for {user.Email}: {isVerified}");
            return isVerified;
        }

        private void SyncUserClaims(int userId, List<string> ldapGroups, string department)
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

            if (IsBilisimDepartment(department))
            {
                targetClaimNames.Add("Admin");
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

        private static bool IsBilisimDepartment(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                return false;
            }

            var normalized = department
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var chars = normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .Select(c => c == 'ı' ? 'i' : c)
                .ToArray();

            return new string(chars) == "bilisim";
        }
    }
}
