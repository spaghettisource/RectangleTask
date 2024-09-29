using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Collections.Concurrent;
using RectangleResizerAPI.Hubs;

namespace RectangleResizerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RectangleController : ControllerBase
    {
        private readonly string _jsonFile = "rectangle.json";
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<Guid, string> _validationResults = new ConcurrentDictionary<Guid, string>();
        private readonly IHubContext<ValidationHub> _hubContext;

        public RectangleController(IHubContext<ValidationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<ActionResult<RectangleModel>> Get()
        {
            if (!System.IO.File.Exists(_jsonFile))
            {
                var defaultRectangle = new RectangleModel { Width = 100, Height = 100 };
                var jsonData = JsonSerializer.Serialize(defaultRectangle);
                await System.IO.File.WriteAllTextAsync(_jsonFile, jsonData);
                return Ok(defaultRectangle);
            }

            var json = await System.IO.File.ReadAllTextAsync(_jsonFile);
            var rectangle = JsonSerializer.Deserialize<RectangleModel>(json);
            return Ok(rectangle);
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] RectangleModel rectangle)
        {
            var validationId = Guid.NewGuid();

            // Start validation without waiting for previous validations to finish
            _ = ValidateAndUpdateRectangleAsync(rectangle, validationId);
            return Ok(new { validationId = validationId });
        }

        private async Task ValidateAndUpdateRectangleAsync(RectangleModel rectangle, Guid validationId)
        {
            // Artificial delay of 10 seconds
            await Task.Delay(10000);

            string errorMessage = string.Empty;

            if (rectangle.Width > rectangle.Height)
            {
                errorMessage = "Width cannot exceed height.";
            }
            else
            {
                // Thread-safe write to the JSON file
                await _semaphore.WaitAsync();
                try
                {
                    var jsonData = JsonSerializer.Serialize(rectangle);
                    await System.IO.File.WriteAllTextAsync(_jsonFile, jsonData);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            // Send validation result to the client via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveValidationResult", new
            {
                validationId = validationId,
                ErrorMessage = errorMessage
            });
        }
    }
}
