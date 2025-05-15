using AccessQueueService.Models;
using AccessQueueService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccessQueueService.Controllers
{
    [ApiController]
    [Route("access")]
    public class AccessController : ControllerBase
    {
        private readonly IAccessService _accessService;

        public AccessController(IAccessService accessService)
        {
            _accessService = accessService;
        }

        [HttpGet]
        [Route("{id}")]
        public async Task<AccessResponse> Get(string id)
        {
            return await _accessService.RequestAccess(id);
        }

        [HttpDelete]
        [Route("{id}")]
        public async Task<bool> Delete(string id)
        {
            return await _accessService.RevokeAccess(id);
        }
    }
}
