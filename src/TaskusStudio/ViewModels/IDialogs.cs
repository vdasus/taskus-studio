namespace TaskusStudio.ViewModels;

public enum SaveChoice { Save, Discard, Cancel }

/// <summary>Windows and pickers the view model asks the view for.</summary>
public interface IDialogs
{
    Task<string?> OpenTaskFileAsync();

    Task<string?> SaveTaskFileAsync(string suggestedName);

    Task<string?> OpenExcelFileAsync();

    Task<string?> OpenAnyFileAsync(string title);

    Task<bool> ConfirmAsync(string title, string message);

    Task<SaveChoice> AskSaveAsync(string fileName);

    Task<string?> ChooseTemplateAsync(IReadOnlyList<TemplateInfo> templates);

    Task ShowSettingsAsync(SettingsViewModel settings);
}
