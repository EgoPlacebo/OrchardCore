using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Data;
using OrchardCore.Recipes.Models;

namespace OrchardCore.SaaS.ViewModels;

public class RegisterUserViewModel
{
    [Required]
    public string UserName { get; set; } = "";

    [Required]
    public string Email { get; set; } = "";

    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    public string PasswordConfirmation { get; set; } = "";
    public required string SiteName { get; set; }
    public required string Handle { get; set; }
    public string? RecipeName { get; set; }
    public string? SiteTimeZone { get; set; }
    public string Secret { get; set; }

    [BindNever]
    public IEnumerable<RecipeDescriptor>? Recipes { get; set; }
}
