using System.ComponentModel.DataAnnotations;

namespace GKit.App1.Components.Account;

public class Credentials
{
    [Required(ErrorMessage = "Required")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Required")]
    public string Password { get; set; } = "";
}
