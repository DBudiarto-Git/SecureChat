namespace SecureChat.WebAPI.DTOs
{
    public record ChangePasswordDto(string Email, string OldPassword, string NewPassword);
}
