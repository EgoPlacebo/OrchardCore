using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Modules;
using OrchardCore.SaaS.ViewModels;
using OrchardCore.Setup.Services;

namespace OrchardCore.SaaS.Controllers;

public class HomeController : Controller
{
    private readonly IClock _clock;
    private readonly IEmailService _smtpService;
    private readonly ISetupService _setupService;
    private readonly ShellSettings _shellSettings;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IShellHost _shellHost;

    public HomeController(
        IClock clock,
        IEmailService emailService,
        ISetupService setupService,
        ShellSettings shellSettings,
        IShellSettingsManager shellSettingsManager,
        IShellHost shellHost)
    {
        _clock = clock;
        _smtpService = emailService;
        _setupService = setupService;
        _shellSettingsManager = shellSettingsManager;
        _shellHost = shellHost;
        _shellSettings = shellSettings;
    }


    public async Task<ActionResult> Index(RegisterUserViewModel viewModel)
    {
        var recipes = await _setupService.GetSetupRecipesAsync();
        var defaultRecipe = recipes.FirstOrDefault(x => x.Tags.Contains("blank")) ?? recipes.FirstOrDefault();
        //viewModel.Recipes = recipes.Where(t => !t.Tags.Contains("developer") && !t.Tags.Contains("headless"));
        //viewModel.RecipeName = defaultRecipe?.Name;
        //viewModel.Secret = Guid.NewGuid().ToString();
        return View(viewModel);
    }

    [HttpPost, ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(RegisterUserViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            var shellSettings = new ShellSettings
            {
                Name = viewModel.SiteName,
                RequestUrlPrefix = viewModel.SiteName,
                RequestUrlHost = "",
                State = TenantState.Uninitialized,
            };
            shellSettings["Secret"] = Guid.NewGuid().ToString();// viewModel.Secret;
            // This should be a setting in the SaaS module.
            shellSettings["DatabaseProvider"] = "Sqlite";
            shellSettings["ConnectionString"] = "";
            shellSettings["TablePrefix"] = viewModel.SiteName;
            shellSettings["RecipeName"] = ""; //viewModel.RecipeName;
            shellSettings["UserName"] = viewModel.UserName; //viewModel.UserName;
            shellSettings["Password"] = "password"; // viewModel.Password;
            shellSettings["SiteTimeZone"] = _clock.GetSystemTimeZone().TimeZoneId;//viewModel.SiteTimeZone;

            await _shellSettingsManager.SaveSettingsAsync(shellSettings);
            var shellContext = await _shellHost.GetOrCreateShellContextAsync(shellSettings);

            var confirmationLink = Url.Action(nameof(HomeController.Confirm), "Home", new { email = viewModel.Email, handle = viewModel.UserName, siteName = viewModel.SiteName }, Request.Scheme);

            var message = new Email.MailMessage
            {
                From = "admin@orchard.com",//, "Orchard SaaS"),
                Subject = "Confirm your email!",
                To = viewModel.Email,
                //IsBodyHtml = true,
                Body = $"Click <a href=\"{HttpUtility.HtmlEncode(confirmationLink).Replace("&amp;", "&")}\">this link</a>"
            };

            //message.To = viewModel.Email; .Add(viewModel.Email);

            await _smtpService.SendAsync(message);

            return RedirectToAction(nameof(Success));
        }
        //viewModel.Recipes = await _setupService.GetSetupRecipesAsync();
        return View(nameof(Index), viewModel);
    }

    public IActionResult Success()
    {
        return View();
    }

    public async Task<IActionResult> Confirm(string email, string handle, string siteName)
    {
        var _shellSettings = await _shellSettingsManager.LoadSettingsAsync(handle);

        if (_shellSettings == null)
        {
            return NotFound();
        }

        var recipes = await _setupService.GetSetupRecipesAsync();
        var selectedRecipe = this._shellSettings["RecipeName"];
        var recipe = recipes.FirstOrDefault(x => x.Tags.Contains(selectedRecipe)) ?? recipes.FirstOrDefault();

        if (recipe == null)
        {
            return NotFound();
        }

        var setupContext = new SetupContext
        {
            ShellSettings = _shellSettings,
            EnabledFeatures = null,
            Errors = new Dictionary<string, string>(),
            Recipe = recipe,
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, siteName },
                { SetupConstants.AdminUsername, "admin" },
                { SetupConstants.AdminEmail, email },
                { SetupConstants.AdminPassword, "P@ssword1" },
                { SetupConstants.SiteTimeZone, _clock.GetSystemTimeZone().TimeZoneId },
            }
        };
        setupContext.Properties[SetupConstants.DatabaseProvider] = this._shellSettings["DatabaseProvider"];
        setupContext.Properties[SetupConstants.DatabaseConnectionString] = this._shellSettings["ConnectionString"];
        setupContext.Properties[SetupConstants.DatabaseTablePrefix] = this._shellSettings["TablePrefix"];
        setupContext.Properties[SetupConstants.DatabaseSchema] = this._shellSettings["Schema"];
        await _setupService.SetupAsync(setupContext);

        // Check if a component in the Setup failed.
        if (setupContext.Errors.Any())
        {
            foreach (var error in setupContext.Errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }

            return Redirect("Error");
        }
        return Redirect("~/" + handle);
    }
}
