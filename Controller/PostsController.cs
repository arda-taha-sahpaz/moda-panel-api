using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ModaPanelApi.models;
using System.Text.Json;

namespace ModaPanelApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _jsonPath;

        public PostsController(IWebHostEnvironment env)
        {
            _env = env;
            _jsonPath = Path.Combine(_env.ContentRootPath, "posts.json");
        }

        private List<Post> ReadPosts()
        {
            if (!System.IO.File.Exists(_jsonPath))
                return new List<Post>();

            var json = System.IO.File.ReadAllText(_jsonPath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<Post>();

            return JsonSerializer.Deserialize<List<Post>>(json) ?? new List<Post>();
        }

        private void SavePosts(List<Post> posts)
        {
            var json = JsonSerializer.Serialize(posts, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            System.IO.File.WriteAllText(_jsonPath, json);
        }

        [HttpGet]
        public IActionResult GetPosts()
        {
            var posts = ReadPosts();
            return Ok(posts);
        }
        
        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Dosya seçilmedi.");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var posts = ReadPosts();

            int newId = posts.Count == 0 ? 1 : posts.Max(x => x.Id) + 1;

            var post = new Post
            {
                Id = newId,
                ImageUrl = $"/uploads/{uniqueFileName}"
            };

            posts.Add(post);
            SavePosts(posts);

            return Ok(post);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var posts = ReadPosts();
            var post = posts.FirstOrDefault(x => x.Id == id);

            if (post == null)
                return NotFound("Foto bulunamadı.");

            var fileName = Path.GetFileName(post.ImageUrl);
            var filePath = Path.Combine(_env.WebRootPath, "uploads", fileName);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            posts.Remove(post);
            SavePosts(posts);

            return Ok(new { message = "Silindi" });
        }
    }
}