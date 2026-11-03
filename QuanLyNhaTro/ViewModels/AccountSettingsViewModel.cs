namespace QuanLyNhaTro.ViewModels;

public sealed class AccountSettingsViewModel
{
    public string Username { get; set; } = string.Empty;
    public UpdateAccountProfileViewModel Profile { get; set; } = new();
    public ChangePasswordViewModel Password { get; set; } = new();
}
