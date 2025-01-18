using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using cloud.core.mongodb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.Linq;
using projectapi.Models;

namespace projectapi.Controllers
{
    [Route("api/")]
    [ApiController]
    public class ProjectApiController(IOptions<JwtSettings> jwtOptions, IOptions<AccountToInitSystem> accountOptions, SampleMongodbConnect db) : ControllerBase
    {

        private readonly JwtSettings _jwtSettings = jwtOptions.Value;
        private readonly AccountToInitSystem _accountSettings = accountOptions.Value;
        private readonly SampleMongodbConnect _db = db;

        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request.Username == _accountSettings.Username && request.Password == _accountSettings.Password)
            {
                var token = GenerateJwtToken();
                return Ok(new { Code = 1, Data = token });
            }

            return Ok(new { Code = 2, Data = "Invalid username or password" });
        }

        private string GenerateJwtToken()
        {
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, _accountSettings.Username ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key ?? ""));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.ExpirationMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        [HttpGet("get-class")]
        public ActionResult<List<ClassEntity>> GetClass()
        {
            var users = _db.NamClass?.ToList();
            return Ok(users);
        }

        // API Thêm mới học sinh
        [HttpPost("create-student")]
        public async Task<IActionResult> AddStudent([FromBody] StudentRequestDto studentRequest)
        {
            // Kiểm tra model validation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Kiểm tra sự tồn tại của ClassId trong collection Classes
            if (!IsValidClass(studentRequest.ClassId.ToString()))
            {
                return BadRequest("The specified ClassId does not exist.");
            }
            // Kiểm tra nếu đường dẫn avatar có hợp lệ không
            if (!string.IsNullOrEmpty(studentRequest.Avatar) && !IsValidImageFormat(studentRequest.Avatar))
            {
                return BadRequest("Avatar image format is invalid. Only jpg, png, jpeg are allowed.");
            }

            // Tạo StudentEntity
            var student = new StudentEntity
            {
                StudentId = Guid.NewGuid(),
                FirstName = studentRequest.FirstName,
                LastName = studentRequest.LastName,
                ClassId = studentRequest.ClassId,
                Gender = studentRequest.Gender,
                DayOfBirth = studentRequest.DayOfBirth,
                Avatar = studentRequest.Avatar
            };

            // Thêm vào database
            await _db.NamStudent!.Insert(student);

            var studentResponse = new StudentResponseDto
            {
                Msg = "Tạo học sinh thành công",
                StudentId = student.StudentId.ToString(),
            };


            return Ok(studentResponse);
        }

        // API Câp nhật học sinh
        [HttpPut("update-student/{studentId}")]
        public async Task<IActionResult> UpdateStudent(string studentId, [FromBody] StudentRequestDto studentRequest)
        {
            if (!IsStudentExist(studentId))
            {
                return BadRequest("Student not found.");
            }
            // Kiểm tra model validation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Kiểm tra sự tồn tại của ClassId trong collection Classes
            if (!IsValidClass(studentRequest.ClassId.ToString()))
            {
                return BadRequest("The specified ClassId does not exist.");
            }
            // Kiểm tra nếu đường dẫn avatar có hợp lệ không
            if (!string.IsNullOrEmpty(studentRequest.Avatar) && !IsValidImageFormat(studentRequest.Avatar))
            {
                return BadRequest("Avatar image format is invalid. Only jpg, png, jpeg are allowed.");
            }

            // Tạo StudentEntity
            var student = new StudentEntity
            {
                StudentId = Guid.NewGuid(),
                FirstName = studentRequest.FirstName,
                LastName = studentRequest.LastName,
                ClassId = studentRequest.ClassId,
                Gender = studentRequest.Gender,
                DayOfBirth = studentRequest.DayOfBirth,
                Avatar = studentRequest.Avatar
            };

            // Thêm vào database
            await _db.NamStudent!.Update(student);

            var studentResponse = new StudentResponseDto
            {
                Msg = "Tạo học sinh thành công",
                StudentId = student.StudentId.ToString(),
            };


            return Ok(studentResponse);
        }

        // API Xoá học sinh
        [HttpDelete("{studentId}")]
        public async Task<IActionResult> DeleteStudent(string studentId)
        {

            if (!IsStudentExist(studentId))
            {
                return BadRequest("Student not found.");
            }
            else
            {
                var studentGuid = Guid.Parse(studentId);
                var student = _db.NamStudent?.Where(u => u.StudentId == studentGuid).FirstOrDefault();
                await _db.NamStudent!.Delete(student!.Id);
            }

            return NoContent();
        }


        // Kiểm tra định dạng ảnh
        private static bool IsValidImageFormat(string avatarUrl)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(avatarUrl)?.ToLower();

            return allowedExtensions.Contains(fileExtension);
        }

        // Kiểm tra lớp có tồn tại không
        private bool IsValidClass(string classId)
        {
            var clazz = _db.NamClass?.Where(u => u.ClassId.ToString() == classId).FirstOrDefault();
            return clazz != null;
        }

        private bool IsStudentExist(string studentId)
        {
            // Chuyển đổi studentId từ string sang Guid
            // var studentGuid = Guid.Parse(studentId);

            // // Tạo BsonBinaryData với Legacy GuidRepresentation
            // var bsonBinaryData = new BsonBinaryData(studentGuid, GuidRepresentation.Standard);

            // Console.WriteLine(studentGuid);

            // var std = _db.NamStudent?.FirstOrDefault();

            // Console.WriteLine(std?.StudentId.ToString());

            // // Console.WriteLine(ConvertLuuidToGuid(std?.StudentId.ToString() ?? ""));

            // var students = _db.NamStudent?.ToList();

            // var student = students?.Where(u => u.StudentId.ToString() == studentId).FirstOrDefault();

            var studentGuid = Guid.Parse(studentId);
            var student = _db.NamStudent?.Where(u => u.StudentId == studentGuid).FirstOrDefault();
            return student != null;
        }
    }

    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class JwtSettings
    {
        public string? Key { get; set; }
        public string? Issuer { get; set; }
        public string? Audience { get; set; }
        public int ExpirationMinutes { get; set; }
        public string? Secret { get; set; }
    }

    public class AccountToInitSystem
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }


    public class ClassEntity : AbstractEntityObjectIdTracking
    {
        public Guid ClassId { get; set; }
        public string Fullname { get; set; } = "";
    }
    public class StudentEntity : AbstractEntityObjectIdTracking
    {
        // public string Guid => Id.ToString();
        [BsonRepresentation(BsonType.Binary)]
        public Guid StudentId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public Guid ClassId { get; set; }
        public short Gender { get; set; } = 1;
        public DateTime? DayOfBirth { get; set; }
        public string Avatar { get; set; } = "";
    }


    public class StudentRequestDto
    {
        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "ClassId is required.")]
        public Guid ClassId { get; set; }

        public short Gender { get; set; } = 1;
        public DateTime? DayOfBirth { get; set; }
        public string Avatar { get; set; } = "";  // Đây là trường chứa đường dẫn hình ảnh
    }

    public class StudentResponseDto
    {
        public string Msg { get; set; } = "";
        public string StudentId { get; set; } = "";
    }
}


