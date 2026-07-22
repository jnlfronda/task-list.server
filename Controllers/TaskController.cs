using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using task_list.server.Data;
using task_list.server.Entities;

namespace task_list.server.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class TaskController(TaskDbContext context) : ControllerBase
{
    private readonly TaskDbContext _dbContext = context;

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("tasks")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasks()
    {
        return await _dbContext.Tasks
            .Where(t => t.UserId == CurrentUserId)
            .ToListAsync();
    }

    [HttpGet("tasks/{id}")]
    public async Task<ActionResult<TaskItem>> GetTask(int id)
    {
        var taskItem = await _dbContext.Tasks
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (taskItem == null)
        {
            return NotFound();
        }
        return taskItem;
    }

    [HttpPost("tasks")]
    public async Task<ActionResult<TaskItem>> CreateTask(TaskItem taskItem)
    {
        taskItem.UserId = CurrentUserId;
        _dbContext.Tasks.Add(taskItem);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTask), new { id = taskItem.Id }, taskItem);
    }

    [HttpPut("tasks/{id}")]
    public async Task<IActionResult> UpdateTask(int id, TaskItem taskItem)
    {
        if (id != taskItem.Id)
        {
            return BadRequest();
        }

        var owned = await _dbContext.Tasks
            .AnyAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (!owned)
        {
            return NotFound();
        }

        taskItem.UserId = CurrentUserId;
        _dbContext.Entry(taskItem).State = EntityState.Modified;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TaskExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }
        return NoContent();
    }

    [HttpDelete("tasks/{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var taskItem = await _dbContext.Tasks
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (taskItem == null)
        {
            return NotFound();
        }
        _dbContext.Tasks.Remove(taskItem);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private bool TaskExists(int id)
    {
        return _dbContext.Tasks.Any(t => t.Id == id && t.UserId == CurrentUserId);
    }
}