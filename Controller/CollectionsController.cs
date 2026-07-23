using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModaPanelApi.models;
using MongoDB.Driver;

namespace ModaPanelApi.Controller
{
    [ApiController]
    [Route("api/collections")]
    public class CollectionsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/webp",
            "image/heic",
            "image/heif"
        };

        private const long MaxFileSize = 15 * 1024 * 1024;
        private const int MaxFilesPerRequest = 30;

        private readonly IMongoCollection<DesignCollection> _collections;
        private readonly Cloudinary _cloudinary;

        public CollectionsController(
            IMongoCollection<DesignCollection> collections,
            Cloudinary cloudinary)
        {
            _collections = collections;
            _cloudinary = cloudinary;
        }

        [HttpGet]
        public async Task<ActionResult<List<DesignCollection>>> GetCollections()
        {
            var collections = await _collections
                .Find(_ => true)
                .SortByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(collections);
        }

        [Authorize]
        [HttpPost]
        [RequestSizeLimit(460_000_000)]
        public async Task<IActionResult> Create(
            [FromForm] string title,
            [FromForm] string? description,
            [FromForm] List<IFormFile>? files)
        {
            title = (title ?? "").Trim();
            description = (description ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { message = "Albüm başlığı zorunludur." });

            if (title.Length > 120)
                return BadRequest(new { message = "Albüm başlığı en fazla 120 karakter olabilir." });

            if (description.Length > 2000)
                return BadRequest(new { message = "Açıklama en fazla 2000 karakter olabilir." });

            var validationError = ValidateFiles(files);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            var images = await UploadFiles(files ?? new List<IFormFile>());
            if (images is null)
                return BadRequest(new { message = "Fotoğraflardan biri yüklenemedi." });

            var collection = new DesignCollection
            {
                Title = title,
                Description = description,
                Images = images
            };

            await _collections.InsertOneAsync(collection);
            return CreatedAtAction(nameof(GetCollections), new { id = collection.Id }, collection);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            string id,
            [FromBody] UpdateDesignCollectionRequest request)
        {
            var title = (request.Title ?? "").Trim();
            var description = (request.Description ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { message = "Albüm başlığı zorunludur." });

            if (title.Length > 120 || description.Length > 2000)
                return BadRequest(new { message = "Başlık veya açıklama çok uzun." });

            var update = Builders<DesignCollection>.Update
                .Set(x => x.Title, title)
                .Set(x => x.Description, description)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collections.UpdateOneAsync(x => x.Id == id, update);
            return result.MatchedCount == 0 ? NotFound() : Ok();
        }

        [Authorize]
        [HttpPost("{id}/images")]
        [RequestSizeLimit(460_000_000)]
        public async Task<IActionResult> AddImages(
            string id,
            [FromForm] List<IFormFile>? files)
        {
            var collectionExists = await _collections.CountDocumentsAsync(x => x.Id == id) > 0;
            if (!collectionExists)
                return NotFound();

            if (files is null || files.Count == 0)
                return BadRequest(new { message = "En az bir fotoğraf seçmelisin." });

            var validationError = ValidateFiles(files);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            var images = await UploadFiles(files);
            if (images is null)
                return BadRequest(new { message = "Fotoğraflardan biri yüklenemedi." });

            var update = Builders<DesignCollection>.Update
                .PushEach(x => x.Images, images)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _collections.UpdateOneAsync(x => x.Id == id, update);
            return Ok(images);
        }

        [Authorize]
        [HttpDelete("{id}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(string id, string imageId)
        {
            var collection = await _collections.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (collection is null)
                return NotFound();

            var image = collection.Images.FirstOrDefault(x => x.Id == imageId);
            if (image is null)
                return NotFound();

            var update = Builders<DesignCollection>.Update
                .PullFilter(x => x.Images, imageItem => imageItem.Id == imageId)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _collections.UpdateOneAsync(x => x.Id == id, update);
            await DeleteCloudinaryImage(image.PublicId);
            return Ok();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCollection(string id)
        {
            var collection = await _collections.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (collection is null)
                return NotFound();

            await _collections.DeleteOneAsync(x => x.Id == id);

            foreach (var image in collection.Images)
                await DeleteCloudinaryImage(image.PublicId);

            return Ok();
        }

        private static string? ValidateFiles(List<IFormFile>? files)
        {
            if (files is null)
                return null;

            if (files.Count > MaxFilesPerRequest)
                return $"Tek seferde en fazla {MaxFilesPerRequest} fotoğraf yükleyebilirsin.";

            foreach (var file in files)
            {
                if (file.Length == 0)
                    return "Boş bir dosya seçildi.";

                if (file.Length > MaxFileSize)
                    return $"{file.FileName} 15 MB sınırını aşıyor.";

                if (!AllowedTypes.Contains(file.ContentType))
                    return $"{file.FileName} desteklenen bir görsel türü değil.";
            }

            return null;
        }

        private async Task<List<DesignImage>?> UploadFiles(List<IFormFile> files)
        {
            var images = new List<DesignImage>();

            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();
                var upload = await _cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "anatolianessence/collections",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false
                });

                if (upload.Error is not null)
                {
                    foreach (var uploadedImage in images)
                        await DeleteCloudinaryImage(uploadedImage.PublicId);

                    return null;
                }

                images.Add(new DesignImage
                {
                    ImageUrl = upload.SecureUrl.ToString(),
                    PublicId = upload.PublicId
                });
            }

            return images;
        }

        private async Task DeleteCloudinaryImage(string? publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return;

            await _cloudinary.DestroyAsync(new DeletionParams(publicId)
            {
                Invalidate = true
            });
        }
    }
}
