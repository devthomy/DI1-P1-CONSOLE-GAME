using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.Http.Json;

using Client.Records;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using Terminal.Gui;

namespace Client.Screens;

// Main class for the Join Game screen
public class JoinGameScreen(Window target)
{
    // Properties and fields to manage the UI and state
    private Window Target { get; } = target;
    private readonly ListView GamesList = new();
    private readonly Button ListReturnButton = new() { Text = "Return" };
    private readonly JoinGameForm Form = new();

    private ICollection<JoinableGame> _joinableGames = [];
    private ICollection<JoinableGame> JoinableGames
    {
        get => _joinableGames;
        set { _joinableGames = value; ReloadListData(); }
    }
    private int? GameId = null;

    // Flags to manage the screen state
    private bool Loading = true;
    private bool Errored = false;
    private bool ListReturned = false;
    private bool FormReturned = false;
    private bool FormSubmitted = false;

    // Main method to display the screen and manage the flow
    public async Task Show()
    {
        await BeforeShow();
        await LoadGames();
        await SelectGame();

        if (ListReturned)
        {
            await Return();
            return;
        }

        await DisplayForm();

        if (FormReturned)
        {
            await SelectGame();
            return;
        }

        await JoinGame();

        if (Errored)
        {
            FormSubmitted = false;
            FormReturned = false;

            await DisplayForm(true);

            if (FormReturned)
            {
                await SelectGame();
                return;
            }

            return;
        }

        // Create and display the current game screen
        var currentGameScreen = new CurrentGameScreen(Target, (int) GameId!, Form.PlayerNameField.Text.ToString()!);
        await currentGameScreen.Show();
    }

    // Initialize the screen before showing
    private Task BeforeShow()
    {
        Target.RemoveAll();
        Target.Title = $"{MainWindow.Title} - [Join Game]";
        return Task.CompletedTask;
    }

    // Return to the main menu
    private async Task Return()
    {
        var mainMenuScreen = new MainMenuScreen(Target);
        await mainMenuScreen.Show();
    }

    // Reload the list data
    private void ReloadListData()
    {
        var dataSource = new JoinGameChoiceListDataSource();
        dataSource.AddRange(JoinableGames);
        GamesList.Source = dataSource;
    }

    // Load available games using SignalR
    private async Task LoadGames()
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri($"{WssConfig.WebSocketServerScheme}://{WssConfig.WebSocketServerDomain}:{WssConfig.WebSocketServerPort}/main"), opts =>
            {
                opts.HttpMessageHandlerFactory = (message) =>
                {
                    if (message is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback +=
                            (sender, certificate, chain, sslPolicyErrors) => { return true; };
                    }

                    return message;
                };
            })
            .AddJsonProtocol()
            .Build();

        hubConnection.On<ICollection<JoinableGame>>("JoinableGamesListUpdated", data =>
        {
            JoinableGames = data;
            Loading = false;
        });

        await hubConnection.StartAsync();

        // Display loading dialog
        var loadingDialog = new Dialog()
        {
            Width = 18,
            Height = 3
        };

        var loadingText = new Label()
        {
            Text = "Loading games...",
            X = Pos.Center(),
            Y = Pos.Center()
        };

        loadingDialog.Add(loadingText);
        Target.Add(loadingDialog);

        while (Loading) { await Task.Delay(100); }

        Target.Remove(loadingDialog);
    }

    // Display the game selection list
    private async Task SelectGame()
    {
        GamesList.X = GamesList.Y = Pos.Center();
        GamesList.Width = JoinableGames.Max(g => g.Name.Length);
        GamesList.Height = Math.Min(JoinableGames.Count, 20);

        ListReturnButton.X = Pos.Center();
        ListReturnButton.Y = Pos.Bottom(GamesList) + 1;
        ListReturnButton.Accept += (_, __) => ListReturned = true;

        Target.Add(GamesList);
        Target.Add(ListReturnButton);

        GamesList.OpenSelectedItem += (_, selected) => GameId = ((JoinableGame) selected.Value).Id;
        GamesList.SetFocus();
        GamesList.MoveHome();

        while (GameId is null && !ListReturned) { await Task.Delay(100); };
    }

    // Display the join game form
    private async Task DisplayForm(bool errored = false)
    {
        Target.RemoveAll();

        Form.OnReturn = (_, __) => FormReturned = true;
        Form.OnSubmit = (_, __) => FormSubmitted = true;

        Form.FormView.X = Form.FormView.Y = Pos.Center();
        Form.FormView.Width = 50;
        Form.FormView.Height = 9;

        Target.Add(Form.FormView);

        while (!FormReturned && !FormSubmitted)
        {
            await Task.Delay(100);
        }
    }

    // Join the selected game
    private async Task JoinGame()
    {
        Target.Remove(Form.FormView);

        // Display loading dialog
        var loadingDialog = new Dialog()
        {
            Width = 17,
            Height = 3
        };

        var loadingText = new Label()
        {
            Text = "Joining game...",
            X = Pos.Center(),
            Y = Pos.Center()
        };

        loadingDialog.Add(loadingText);
        Target.Add(loadingDialog);

        // Set up HTTP client
        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true
        };

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri($"{WssConfig.WebApiServerScheme}://{WssConfig.WebApiServerDomain}:{WssConfig.WebApiServerPort}"),
        };

        var playerName = Form.PlayerNameField.Text.ToString();
        var companyName = Form.CompanyNameField.Text.ToString();

        var requestBody = new { playerName, companyName };
        var request = httpClient.PostAsJsonAsync($"/games/{GameId}/join", requestBody);
        var response = await request;

        if (!response.IsSuccessStatusCode)
        {
            Errored = true;
        }
    }
}

// Class to manage the data source for the game list
public class JoinGameChoiceListDataSource : List<JoinableGame>, IListDataSource
{
    public int Length => Count;

    public bool SuspendCollectionChangedEvent { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public event NotifyCollectionChangedEventHandler CollectionChanged = (_, __) => { };

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public bool IsMarked(int item)
    {
        return false;
    }

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0)
    {
        var game = this[item];
        driver.AddStr($"{game.Name} ({game.PlayersCount}/{game.MaximumPlayersCount})");
    }

    public void SetMark(int item, bool value) { }

    public IList ToList()
    {
        return this;
    }
}

// Class to manage the join game form
public class JoinGameForm
{
    // Properties and events to manage the form
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

    // UI elements of the form
    public View FormView { get; }
    public View ButtonsView { get; }
    public Button SubmitButton { get; }
    public Button ReturnButton { get; }
    public Label PlayerNameLabel { get; }
    public Label CompanyNameLabel { get; }
    public TextField PlayerNameField { get; }
    public TextField CompanyNameField { get; }

    // Constructor to initialize the form
    public JoinGameForm()
    {
        PlayerNameLabel = new Label()
        {
            X = 0,
            Y = 0,
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

        ButtonsView = new View()
        {
            Width = 1,
            Height = 1,
            X = Pos.Center(),
            Y = Pos.Bottom(CompanyNameLabel) + 1
        };

        SubmitButton = new Button()
        {
            Text = "Submit",
            IsDefault = true
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
            PlayerNameLabel, CompanyNameLabel,
            PlayerNameField, CompanyNameField,
            ButtonsView
        );
    }
}