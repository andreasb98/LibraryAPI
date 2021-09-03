using LibraryAPI.Configuration;
using LibraryAPI.Model;
using LibraryAPI.Model.DTOs.Requests;
using LibraryAPI.Model.DTOs.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LibraryAPI.Controllers
{
    [Route("api/[controller]")] // api/authManagement
    [ApiController]
    public class AuthManagementController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JwtConfig _jwtConfig;
        private readonly TokenValidationParameters _tokenValidationParams;
        private readonly MemberContext _memberContext;

        public AuthManagementController(
            UserManager<IdentityUser> userManager, 
            IOptionsMonitor<JwtConfig> optionsMonitor,
            TokenValidationParameters tokenValidationParams,
            MemberContext memberContext)
        {
            _userManager = userManager;
            _jwtConfig = optionsMonitor.CurrentValue;
            _tokenValidationParams = tokenValidationParams;
            _memberContext = memberContext;
        }
        //Registration with authentication, JWT as token
        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Email);
                if(existingUser != null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>()
                {
                    "Email already in use"
                },
                        IsSuccess = false
                    });
                }

                var newUser = new IdentityUser() { Email = user.Email, UserName = user.Name, PhoneNumber = user.Mobile};
                var isCreated = await _userManager.CreateAsync(newUser, user.password);
                if (isCreated.Succeeded)
                {
                    var jwtToken = await GenerateJwtToken(newUser);
                    return Ok(jwtToken);
                }else
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = isCreated.Errors.Select(x => x.Description).ToList(),                   
                        IsSuccess = false
                    });
                }
            }
            return BadRequest(new RegistrationResponse()
            {
                Errors = new List<string>()
                {
                    "Invalid payload"
                },
                IsSuccess = false
            });
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequestDto user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Email);
                if(existingUser == null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>()
                {
                    "Invalid Login request"
                },
                        IsSuccess = false
                    });
                }
                
                var isCorrect = await _userManager.CheckPasswordAsync(existingUser, user.Password);
                if (!isCorrect)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>()
                {
                    "Invalid Login request"
                },
                        IsSuccess = false
                    });
                }               
                var jwtToken = await GenerateJwtToken(existingUser);                
                return Ok(jwtToken);
                
            }
            return BadRequest(new RegistrationResponse()
            {
                Errors = new List<string>()
                {
                    "Invalid payload"
                },
                IsSuccess = false
            });
        }

        [HttpPost]
        [Route("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRequest tokenRequest)
        {
            if (ModelState.IsValid)
            {
                var result = await VerifyAndGenerateToken(tokenRequest);
                if (result == null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>()
                    {
                        "Invalid tokens"
                    },
                        IsSuccess = false
                    });
                }

                return Ok(result);
            }
            return BadRequest(new RegistrationResponse()
            {
                Errors = new List<string>()
                {
                    "Invalid payload"
                },
                IsSuccess = false
            });
        }

        private async Task<AuthResult> GenerateJwtToken(IdentityUser user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtConfig.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("Id", user.Id),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Expires = DateTime.UtcNow.AddSeconds(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = jwtTokenHandler.WriteToken(token);

            var refreshToken = new RefreshToken()
            {
                JwtId = token.Id,
                IsUsed = false,
                IsRevoked = false,
                UserId = user.Id,
                AddedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                Token = RandomString(35) + Guid.NewGuid()
            };

            await _memberContext.RefreshTokens.AddAsync(refreshToken);
            await _memberContext.SaveChangesAsync();

            return new AuthResult()
            {
                Token = jwtToken,
                IsSuccess = true,
                RefreshToken = refreshToken.Token
            };
        }

        //Verifying that the token beongs to this application
        private async Task<AuthResult> VerifyAndGenerateToken(TokenRequest tokenRequest)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            try
            {
                //validation 1 - is the token in the right format?
                var tokenInVerification = jwtTokenHandler.ValidateToken(
                    tokenRequest.Token, _tokenValidationParams, out var validatedToken);

                //validation 2 - does the token have the right encryption?
                if(validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(
                        SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);
                    if(result == false)
                    {
                        return null;
                    }
                }

                //Validation 3 - has the token expired?
                var utcExpiryDate = long.Parse(
                    tokenInVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp).Value);

                var expiryDate = UnixTimeStampToDateTime(utcExpiryDate);

                if(expiryDate > DateTime.UtcNow)
                {
                    return new AuthResult()
                    {
                        IsSuccess = false,
                        Token = tokenRequest.Token,
                        RefreshToken = tokenRequest.RefreshToken,
                        Errors = new List<string>()
                        {
                            "Token has not yet expired"
                        }
                    };
                }

                //Validation 4 - does the token exist in the db?
                var storedToken = await _memberContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == tokenRequest.RefreshToken);
                if (storedToken == null)
                {
                    return new AuthResult()
                    {
                        IsSuccess = false,
                        Errors = new List<string>()
                        {
                            "Token does not exist"
                        }
                    };
                }

                //Validation 5 - has the token been used before?
                if (storedToken.IsUsed)
                {
                    return new AuthResult()
                    {
                        IsSuccess = false,
                        Errors = new List<string>()
                        {
                            "Token has been used"
                        }
                    };
                }

                //Validation 6 - has the token been revoked?
                if (storedToken.IsRevoked)
                {
                    return new AuthResult()
                    {
                        IsSuccess = false,
                        Errors = new List<string>()
                        {
                            "Token has been revoked"
                        }
                    };
                }

                //Validation 7 - Does the jti (jwt token id) match the refreshtoken ID in the db?
                var jti = tokenInVerification.Claims.FirstOrDefault(x=> x.Type == JwtRegisteredClaimNames.Jti).Value;
                if(storedToken.JwtId != jti)
                {
                    return new AuthResult()
                    {
                        IsSuccess = false,
                        Errors = new List<string>()
                        {
                            "Token does not match"
                        }
                    };
                }

                //Update current token
                storedToken.IsUsed = true;
                _memberContext.RefreshTokens.Update(storedToken);
                await _memberContext.SaveChangesAsync();

                //Generating a new token
                var dbUser = await _userManager.FindByIdAsync(storedToken.UserId);
                return await GenerateJwtToken(dbUser);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Lifetime validation failed. The token is expired."))
                {

                    return new AuthResult()
                    {
                        IsSuccess = false,
                        Errors = new List<string>() {
                            "Token has expired please re-login"
                        }
                    };

                }
                else
                {
                    return new AuthResult()
                    {
                        IsSuccess = false,
                        Errors = new List<string>() {
                            "Something went wrong."
                        }
                    };
                }
            }
        }

        private string RandomString(int length)
        {
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
               .Select(x => x[random.Next(x.Length)]).ToArray());
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTimeVal;
        }
    }
}
