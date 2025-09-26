namespace Gateway.API.Dtos;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Role { get; set; } = null!;
}
