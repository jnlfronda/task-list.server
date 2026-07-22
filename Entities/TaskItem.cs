using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace task_list.server.Entities;

[Table("Tasks")]
public class TaskItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Column("due_date")]
    public DateTime DueDate { get; set; }

    [Required]
    public string Priority { get; set; } = string.Empty;

    public string? Category { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;
}