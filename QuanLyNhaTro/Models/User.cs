using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class User
{
    [Key]
    public int UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;
}
