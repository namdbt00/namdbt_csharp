using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using cloud.core.mongodb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [HttpPost("v1/login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request.Username == _accountSettings.Username && request.Password == _accountSettings.Password)
            {
                var token = GenerateJwtToken();
                return Ok(new { Code = 1, Data = token });
            }

            return Ok(new { Code = 2, Data = "Thông tin đăng nhập không chính xác." });
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


        [HttpGet("v1/class/load")]
        public ActionResult<List<ClassEntity>> GetClass()
        {
            var users = _db.NamClass.ToList();
            return Ok(users);
        }


        [HttpGet("v1/student/load")]
        public IActionResult GetStudents([FromQuery] int page = 1, [FromQuery] int pageSize = 45, [FromQuery] Guid? classId = null)
        {
            try
            {
                var skip = (page - 1) * pageSize;

                var query = _db.NamStudent.AsQueryable();

                // Lọc theo classId nếu có
                if (classId.HasValue)
                {
                    query = query.Where(student => student.ClassId == classId.Value);
                }

                var totalStudents = query.Count();

                // Phân trang và sắp xếp
                var studentList = query
                    // Sắp xếp tiếng Việt
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList();

                studentList = [.. studentList.OrderBy(student => student.LastName, StringComparer.Create(new CultureInfo("vi-VN"), ignoreCase: true))];

                return Ok(new
                {
                    totalItems = totalStudents,
                    totalPages = (int)Math.Ceiling((double)totalStudents / pageSize),
                    currentPage = page,
                    pagesize = pageSize,
                    data = studentList
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // API Thêm mới học sinh
        [HttpPost("v1/student/create")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentRequestDto studentRequest)
        {
            try
            {
                // Kiểm tra model validation
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                // Kiểm tra sự tồn tại của ClassId trong collection Classes
                if (!IsValidClass(studentRequest.ClassId.ToString()))
                {
                    return BadRequest("ClassId không hợp lệ.");
                }
                // Kiểm tra nếu đường dẫn avatar có hợp lệ không
                if (!string.IsNullOrEmpty(studentRequest.Avatar) && !IsValidImageFormat(studentRequest.Avatar))
                {
                    return BadRequest("Avatar phải có dạng jpg/png/jpeg.");
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
                await _db.NamStudent.Insert(student);

                var studentResponse = new StudentResponseDto
                {
                    Msg = "Tạo học sinh thành công.",
                    StudentId = student.StudentId.ToString(),
                };


                return Ok(studentResponse);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // API Câp nhật học sinh
        [HttpPut("v1/student/update/{studentId}")]
        public async Task<IActionResult> UpdateStudent(string studentId, [FromBody] StudentRequestDto studentRequest)
        {
            try
            {

                if (!IsStudentExist(studentId))
                {
                    return BadRequest("Không tìm thấy học sinh.");
                }
                // Kiểm tra model validation
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                // Kiểm tra sự tồn tại của ClassId trong collection Classes
                if (!IsValidClass(studentRequest.ClassId.ToString()))
                {
                    return BadRequest("ClassId không hợp lệ.");
                }
                // Kiểm tra nếu đường dẫn avatar có hợp lệ không
                if (!string.IsNullOrEmpty(studentRequest.Avatar) && !IsValidImageFormat(studentRequest.Avatar))
                {
                    return BadRequest("Avatar phải có dạng jpg/png/jpeg.");
                }

                // Tạo StudentEntity
                var studentGuid = Guid.Parse(studentId);
                var existingStudent = _db.NamStudent.Where(u => u.StudentId == studentGuid).FirstOrDefault();

                // Cập nhật thông tin học sinh
                existingStudent!.FirstName = studentRequest.FirstName;
                existingStudent.LastName = studentRequest.LastName;
                existingStudent.ClassId = studentRequest.ClassId;
                existingStudent.Gender = studentRequest.Gender != 0 ? studentRequest.Gender : existingStudent.Gender;;
                existingStudent.DayOfBirth = studentRequest.DayOfBirth ?? existingStudent.DayOfBirth;
                existingStudent.Avatar = studentRequest.Avatar ?? existingStudent.Avatar;


                // Thêm vào database
                await _db.NamStudent.Update(existingStudent);

                var studentResponse = new StudentResponseDto
                {
                    Msg = "Cập nhật học sinh thành công.",
                    StudentId = existingStudent.StudentId.ToString(),
                };
                return Ok(studentResponse);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // API Xoá học sinh
        [HttpDelete("v1/student/delete/{studentId}")]
        public async Task<IActionResult> DeleteStudent(string studentId)
        {

            try
            {
                if (!IsStudentExist(studentId))
                {
                    return BadRequest("Không tìm thấy học sinh.");
                }
                else
                {
                    var studentGuid = Guid.Parse(studentId);
                    var student = _db.NamStudent.Where(u => u.StudentId == studentGuid).FirstOrDefault();
                    await _db.NamStudent.Delete(student!.Id);
                }
                var studentResponse = new StudentResponseDto
                {
                    Msg = "Xoá học sinh thành công.",
                };
                return Ok(studentResponse);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
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
            var clazz = _db.NamClass.Where(u => u.ClassId.ToString() == classId).FirstOrDefault();
            return clazz != null;
        }

        private bool IsStudentExist(string studentId)
        {
            var studentGuid = Guid.Parse(studentId);
            var student = _db.NamStudent.Where(u => u.StudentId == studentGuid).FirstOrDefault();
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


