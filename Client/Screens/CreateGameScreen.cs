using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;

using JetBrains.Annotations;

using Terminal.Gui;

namespace Client.Screens;
public class CreateGameScreen(Window target)
{
    private Window Target { get; } = target;
    private readonly CreateGameForm Form = new();

    private int? GameId = null;

    private bool Submitted = false;
    private bool Returned = false;
    private bool Errored = false;


    public async Task Show()
    {
        await BeforeShow();
        await DisplayForm();

        if (Returned)
        {
            await Return();
            return;
        }

        await CreateGame();

        if (Errored)
        {
            Submitted = false;
            Returned = false;

            await DisplayForm(true);

            if (Returned)
            {
                await Return();
                return;
            }

            return;
        }

        var currentGameScreen = new CurrentGameScreen(Target, (int) GameId!, Form.PlayerNameField.Text.ToString()!);

        await currentGameScreen.Show();
    }

    private Task BeforeShow()
    {
        Target.RemoveAll();
        Target.Title = $"{MainWindow.Title} - [Create Game]";
        return Task.CompletedTask;
    }

    private async Task Return()
    {
        var mainMenuScreen = new MainMenuScreen(Target);
        await mainMenuScreen.Show();
    }

    private async Task DisplayForm(bool errored = false)
    {
        Form.OnReturn = (_, __) => Returned = true;
        Form.OnSubmit = (_, __) => Submitted = true;

        Form.FormView.X = Form.FormView.Y = Pos.Center();
        Form.FormView.Width = 50;
        Form.FormView.Height = 9;

        Target.Add(Form.FormView);

        while (!Returned && !Submitted)
        {
            await Task.Delay(100);
        }
    }

    public class FormValidator
    {
        // Méthode pour vérifier si tous les champs sont remplis
        public bool AreFieldsFilled(string gameName, string playerName, string companyName)
        {
            return !string.IsNullOrEmpty(gameName) && 
                !string.IsNullOrEmpty(playerName) && 
                !string.IsNullOrEmpty(companyName);
        }
    }

    private async Task CreateGame()
    {
        Target.Remove(Form.FormView);

        var loadingDialog = new Dialog()
        {
            Width = 18,
            Height = 3
        };

        var loadingText = new Label()
        {
            Text = "Creating game...",
            X = Pos.Center(),
            Y = Pos.Center()
        };

        loadingDialog.Add(loadingText);

        Target.Add(loadingDialog);

        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true
        };

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri($"{WssConfig.WebApiServerScheme}://{WssConfig.WebApiServerDomain}:{WssConfig.WebApiServerPort}"),
        };

     
        var rounds = int.Parse(Form.RoundsField.Text.ToString()!);
        var gameName = Form.GameNameField.Text.Trim();
        var playerName = Form.PlayerNameField.Text.Trim();
        var companyName = Form.CompanyNameField.Text.Trim();        
        var requestBody = new { gameName, playerName, companyName, rounds };
        var request = httpClient.PostAsJsonAsync("/games", requestBody);
        var response = await request;

        if (!response.IsSuccessStatusCode)
        {
            Errored = true;
        }
        else
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            GameId = content.GetProperty("id").GetInt32();
        }
    }
}

public class CreateGameForm
{
    private EventHandler<HandledEventArgs> _onSubmit = (_, __) => { };
    private EventHandler<HandledEventArgs> _onReturn = (_, __) => { };

    public EventHandler<HandledEventArgs> OnSubmit
    {
        get => _onSubmit;
        set
        {
            SubmitButton.Accept -= _onSubmit;
            SubmitButton.Accept += value;
            _onSubmit = value;
        }
    }
    public EventHandler<HandledEventArgs> OnReturn
    {
        get => _onReturn;
        set
        {
            ReturnButton.Accept -= _onReturn;
            ReturnButton.Accept += value;
            _onReturn = value;
        }
    }

    public View FormView { get; }
    public View ButtonsView { get; }
    public Button SubmitButton { get; }
    public Button ReturnButton { get; }
    public Label GameNameLabel { get; }
    public Label PlayerNameLabel { get; }
    public Label CompanyNameLabel { get; }
    public Label RoundsLabel { get; }
    public TextField GameNameField { get; }
    public TextField PlayerNameField { get; }
    public TextField CompanyNameField { get; }
    public TextField RoundsField { get; }

    public CreateGameForm()
{
    GameNameLabel = new Label()
    {
        X = 0,
        Y = 0,
        Width = 20,
        Text = "Game name :"
    };

    PlayerNameLabel = new Label()
    {
        X = Pos.Left(GameNameLabel),
        Y = Pos.Bottom(GameNameLabel) + 1,
        Width = 20,
        Text = "Player name :"
    };

    CompanyNameLabel = new Label()
    {
        X = Pos.Left(PlayerNameLabel),
        Y = Pos.Bottom(PlayerNameLabel) + 1,
        Width = 20,
        Text = "Company name :"
    };

    RoundsLabel = new Label()
    {
        X = Pos.Left(CompanyNameLabel),
        Y = Pos.Bottom(CompanyNameLabel) + 1,
        Width = 20,
        Text = "Rounds (1 - 100) :"
    };

    GameNameField = new TextField()
    {
        X = Pos.Right(GameNameLabel),
        Y = Pos.Top(GameNameLabel),
        Width = Dim.Fill(),
        Text = ""
    };

    PlayerNameField = new TextField()
    {
        X = Pos.Right(PlayerNameLabel),
        Y = Pos.Top(PlayerNameLabel),
        Width = Dim.Fill(),
        Text = ""
    };

    CompanyNameField = new TextField()
    {
        X = Pos.Right(CompanyNameLabel),
        Y = Pos.Top(CompanyNameLabel),
        Width = Dim.Fill(),
        Text = ""
    };

    RoundsField = new TextField()
    {
        X = Pos.Right(RoundsLabel),
        Y = Pos.Top(RoundsLabel),
        Width = Dim.Fill(),
        Text = "",
        
    };

    // Ajoutez les événements de changement de texte
    GameNameField.TextChanged += OnFieldTextChanged;
    PlayerNameField.TextChanged += OnFieldTextChanged;
    CompanyNameField.TextChanged += OnFieldTextChanged;
    RoundsField.TextChanged += OnFieldTextChanged;

    RoundsField.TextChanged += OnRoundsFieldTextChanged;


    ButtonsView = new View()
    {
        Width = 1,
        Height = 1,
        X = Pos.Center(),
        Y = Pos.Bottom(RoundsLabel) + 1
    };

    SubmitButton = new Button()
    {
        Text = "Submit",
        IsDefault = true,
        Visible = false // Masquer le bouton au départ
    };

    ReturnButton = new Button()
    {
        Text = "Return",
        IsDefault = false,
        X = Pos.Right(SubmitButton) + 1
    };

    SubmitButton.Accept += OnSubmit;
    ReturnButton.Accept += OnReturn;

    ButtonsView.Add(SubmitButton, ReturnButton);

    var submitButtonWidth = SubmitButton.Width;
    var returnButtonWidth = ReturnButton.Width;

    ButtonsView.Width = submitButtonWidth + returnButtonWidth + 1;

    FormView = new View()
    {
        Width = Dim.Fill(),
        Height = Dim.Fill()
    };

    FormView.Add(
        GameNameLabel, PlayerNameLabel, CompanyNameLabel, RoundsLabel,
        GameNameField, PlayerNameField, CompanyNameField, RoundsField,
        ButtonsView
    );
}

private void OnFieldTextChanged(object? sender, EventArgs e)
{
    UpdateSubmitButtonVisibility();
}

private void UpdateSubmitButtonVisibility()
{
    // Vérifie si tous les champs sont remplis
    bool allFieldsFilled = !string.IsNullOrEmpty(GameNameField.Text) &&
                           !string.IsNullOrEmpty(PlayerNameField.Text) &&
                           !string.IsNullOrEmpty(CompanyNameField.Text) &&
                           !string.IsNullOrEmpty(RoundsField.Text);


    SubmitButton.Visible = allFieldsFilled; 
    }
    private void OnRoundsFieldTextChanged(object? sender, EventArgs e)
{
    string text = RoundsField.Text.ToString()!;

    string numericText = new string(text.Where(char.IsDigit).ToArray());

    if (int.TryParse(numericText, out int value))
    {
        if (value > 100)
        {
            numericText = "100"; 
        }
        if (value <15 && numericText.Length>1)
        {
            numericText = "15";
        }
    }

    if (text != numericText)
    {
        RoundsField.Text = numericText;
        RoundsField.CursorPosition = numericText.Length;
    }
}

}



