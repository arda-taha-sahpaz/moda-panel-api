using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using ModaPanelApi.models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ModaPanelApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IMongoCollection<Post> _posts;
        private readonly Cloudinary _cloudinary;

        public PostsController(IMongoCollection<Post> posts, Cloudinary cloudinary)
        {
            _posts = posts;
            _cloudinary = cloudinary;
        }

        [HttpGet]
        public async Task<IActionResult> GetPosts()
        {
            var posts = await _posts.Find(_ => true).ToListAsync();
            return Ok(posts);
        }

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload()
        {
            var file = Request.Form.Files.FirstOrDefault();

            if (file == null || file.Length == 0)
                return BadRequest("Dosya yok");

            using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "anatolianessence"
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                return BadRequest(result.Error.Message);

            var post = new Post
            {
                ImageUrl = result.SecureUrl.ToString()
            };

            await _posts.InsertOneAsync(post);

            return Ok(post);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _posts.DeleteOneAsync(x => x.Id == id);
            return Ok();
        }
    }
}