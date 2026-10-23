using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(IBookingService bookings) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request)
    {
        var booking = await bookings.CreateAsync(User.GetUserId(), request);
        return CreatedAtAction(nameof(GetMine), new { }, booking);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await bookings.CancelAsync(User.GetUserId(), User.IsAdmin(), id);
        return NoContent();
    }

    [HttpGet("mine")]
    public Task<IReadOnlyList<BookingResponse>> GetMine() => bookings.GetMineAsync(User.GetUserId());
}
