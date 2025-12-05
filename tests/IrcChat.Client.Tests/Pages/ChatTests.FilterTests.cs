// tests/IrcChat.Client.Tests/Pages/ChatTests.FilterTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Pages;

/// <summary>
/// Tests unitaires pour le filtre global dans Chat.razor
/// Classe partielle qui étend ChatTests.cs.
/// </summary>
public partial class ChatTests
{
    [Fact]
    public async Task Chat_GlobalFilter_WhenEmpty_ShouldShowAllChannels()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "gaming", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Assert
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
        Assert.Contains("gaming", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_WithChannelName_ShouldFilterChannels()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "gaming", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act - Ouvrir le filtre et saisir "gen"
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("gen"));
        await cut.InvokeAsync(() => filterInput.KeyUp("n"));
        await Task.Delay(100);
        cut.Render();

        // Assert - Seul "general" devrait être visible
        Assert.Contains("general", cut.Markup);
        Assert.DoesNotContain("random", cut.Markup);
        Assert.DoesNotContain("gaming", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_CaseInsensitive_ShouldWork()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "General", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "RANDOM", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "General");
        cut.Render();

        // Act - Filtrer avec "general" en minuscules
        await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn").Click());
        cut.Render();

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        filterInput.Input("general");
        await filterInput.KeyUpAsync("l");
        cut.Render();

        // Assert - "General" devrait être trouvé malgré la différence de casse
        Assert.Contains("General", cut.Markup);
        Assert.DoesNotContain("RANDOM", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_NoMatch_ShouldShowEmptyList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act - Filtrer avec un texte qui ne correspond à rien
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);
        cut.Render();

        var channelList = await cut.InvokeAsync(() => cut.FindAll(".channels-section .channel-list li"));
        Assert.NotEmpty(channelList);

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("xyz123"));
        await cut.InvokeAsync(() => filterInput.KeyUp("3"));
        await Task.Delay(100);
        cut.Render();

        // Assert - Aucun salon ne devrait être affiché
        channelList = await cut.InvokeAsync(() => cut.FindAll(".channels-section .channel-list li"));
        Assert.Empty(channelList);
    }

    [Fact]
    public async Task Chat_GlobalFilter_WithUsername_ShouldFilterUsers()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var users = new List<User>
        {
            new() { UserId = "1", Username = "alice" },
            new() { UserId = "2", Username = "bob" },
            new() { UserId = "3", Username = "charlie" },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general"))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Ouvrir la liste des utilisateurs
        var usersToggleBtn = await cut.InvokeAsync(() => cut.Find(".users-toggle-btn"));
        await cut.InvokeAsync(() => usersToggleBtn.Click());
        await Task.Delay(100);
        cut.Render();

        // Ouvrir le filtre et saisir "al"
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);
        cut.Render();

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("al"));
        await cut.InvokeAsync(() => filterInput.KeyUp("l"));
        await Task.Delay(100);
        cut.Render();

        // Assert - Seul "alice" devrait être visible
        Assert.Contains("alice", cut.Markup);
        Assert.DoesNotContain("bob", cut.Markup);
        Assert.DoesNotContain("charlie", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_Users_CaseInsensitive_ShouldWork()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var users = new List<User>
        {
            new() { UserId = "1", Username = "Alice" },
            new() { UserId = "2", Username = "BOB" },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general"))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act
        var usersToggleBtn = await cut.InvokeAsync(() => cut.Find(".users-toggle-btn"));
        await cut.InvokeAsync(() => usersToggleBtn.Click());
        await Task.Delay(100);
        cut.Render();

        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);
        cut.Render();

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("alice"));
        await cut.InvokeAsync(() => filterInput.KeyUp("e"));
        await Task.Delay(100);
        cut.Render();

        // Assert - "Alice" devrait être trouvé
        Assert.Contains("Alice", cut.Markup);
        Assert.DoesNotContain("BOB", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_WithConversationUsername_ShouldFilterConversations()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "1", Username = "alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 1 },
            new() { OtherUser = new User { UserId = "2", Username = "bob" }, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 },
            new() { OtherUser = new User { UserId = "3", Username = "charlie" }, LastMessage = "Hey", LastMessageTime = DateTime.UtcNow, UnreadCount = 2 },
        };

        privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);
        privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "1", "alice");
        await Task.Delay(200);
        cut.Render();

        // Act - Ouvrir le filtre et saisir "bob"
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);
        cut.Render();

        var privateConversationsList = await cut.InvokeAsync(() => cut.FindAll(".private-conversations .conversation-list li"));
        Assert.NotEmpty(privateConversationsList);

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("bob"));
        await cut.InvokeAsync(() => filterInput.KeyUp("b"));
        await Task.Delay(100);
        cut.Render();

        // Assert - Seule la conversation avec "bob" devrait être visible
        privateConversationsList = await cut.InvokeAsync(() => cut.FindAll(".private-conversations .conversation-list li"));
        Assert.NotEmpty(privateConversationsList);
        Assert.Contains(privateConversationsList, c => c.TextContent.Contains("bob"));
        Assert.DoesNotContain(privateConversationsList, c => c.TextContent.Contains("alice"));
        Assert.DoesNotContain(privateConversationsList, c => c.TextContent.Contains("charlie"));
    }

    [Fact]
    public async Task Chat_GlobalFilter_Conversations_CaseInsensitive_ShouldWork()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "1", Username = "ALICE" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 1 },
            new() { OtherUser = new User { UserId = "2", Username = "Bob" }, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 },
        };

        privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);
        privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "1", "ALICE");
        await Task.Delay(200);
        cut.Render();

        // Act - Filtrer avec "alice" en minuscules
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);
        cut.Render();

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("alice"));
        await cut.InvokeAsync(() => filterInput.KeyUp("e"));
        await Task.Delay(100);
        cut.Render();

        // Assert - "ALICE" devrait être trouvé
        Assert.Contains("ALICE", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_ShouldFilterAllListsSimultaneously()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "test-channel", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var users = new List<User>
        {
            new() { UserId = "1", Username = "tester" },
            new() { UserId = "2", Username = "alice" },
        };
        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "1", Username = "test_user" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 1 },
            new() { OtherUser = new User { UserId = "2", Username = "bob" }, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/test-channel*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/test-channel/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("test-channel"))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        var cut = await RenderChatAsync(channelName: "test-channel");

        // Ouvrir la liste des utilisateurs
        var usersToggleBtn = await cut.InvokeAsync(() => cut.Find(".users-toggle-btn"));
        await cut.InvokeAsync(() => usersToggleBtn.Click());
        await Task.Delay(100);

        // Act - Filtrer avec "test"
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("test"));
        await cut.InvokeAsync(() => filterInput.KeyUp("t"));
        await Task.Delay(100);
        cut.Render();

        // Assert - Les 3 éléments contenant "test" devraient être visibles
        Assert.Contains("test-channel", cut.Markup); // Salon
        Assert.Contains("tester", cut.Markup); // Utilisateur
        Assert.Contains("test_user", cut.Markup); // Conversation

        // Et les autres ne devraient pas être visibles
        Assert.DoesNotContain("general", cut.Markup);
        Assert.DoesNotContain("alice", cut.Markup);
        Assert.DoesNotContain("bob", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_ClearFilter_ShouldRestoreAllLists()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act - Filtrer puis effacer
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("gen"));
        await cut.InvokeAsync(() => filterInput.KeyUp("n"));
        await Task.Delay(100);
        cut.Render();

        // Vérifier que le filtre fonctionne
        Assert.Contains("general", cut.Markup);
        Assert.DoesNotContain("random", cut.Markup);

        // Effacer le filtre
        filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input(string.Empty));
        await cut.InvokeAsync(() => filterInput.KeyUp(" "));
        await Task.Delay(100);
        cut.Render();

        // Assert - Tous les salons devraient réapparaître
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_PersistsAcrossChannelChanges()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var users1 = new List<User>
        {
            new() { UserId = "1", Username = "alice" },
            new() { UserId = "2", Username = "bob" },
        };
        var users2 = new List<User>
        {
            new() { UserId = "3", Username = "charlie" },
            new() { UserId = "4", Username = "david" },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/messages/random*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users1));
        mockHttp.When(HttpMethod.Get, "*/api/channels/random/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users2));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Ouvrir la liste des utilisateurs
        var usersToggleBtn = await cut.InvokeAsync(() => cut.Find(".users-toggle-btn"));
        await cut.InvokeAsync(() => usersToggleBtn.Click());
        await Task.Delay(100);

        // Act - Activer un filtre
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("ali"));
        await cut.InvokeAsync(() => filterInput.KeyUp("i"));
        await Task.Delay(100);
        cut.Render();

        // Vérifier que le filtre fonctionne dans #general
        Assert.Contains("alice", cut.Markup);
        Assert.DoesNotContain("bob", cut.Markup);

        // Changer de salon vers #random
        await NavigateToChannelAsync(cut, "random");
        await Task.Delay(200);
        cut.Render();

        // Assert - Le filtre devrait persister (aucun utilisateur ne contient "ali" dans #random)
        Assert.DoesNotContain("charlie", cut.Markup);
        Assert.DoesNotContain("david", cut.Markup);
    }

    [Fact]
    public async Task Chat_GlobalFilter_WorksInPrivateConversations()
    {
        // Arrange
        SetupBasicAuth();
        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "1", Username = "alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 },
            new() { OtherUser = new User { UserId = "2", Username = "Friend" }, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 },
        };

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);
        privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");
        await Task.Delay(200);

        // Act - Activer un filtre dans une conversation privée
        var filterButton = await cut.InvokeAsync(() => cut.Find(".filter-toggle-btn"));
        await cut.InvokeAsync(() => filterButton.Click());
        await Task.Delay(100);

        var filterInput = await cut.InvokeAsync(() => cut.Find(".filter-input"));
        await cut.InvokeAsync(() => filterInput.Input("alice"));
        await cut.InvokeAsync(() => filterInput.KeyUp("e"));
        await Task.Delay(100);
        cut.Render();

        // Assert - Le filtre devrait fonctionner (sidebar devrait montrer seulement alice)
        var privateConversationsList = await cut.InvokeAsync(() => cut.FindAll(".private-conversations .conversation-list li"));

        Assert.Contains(privateConversationsList, c => c.TextContent.Contains("alice"));
        Assert.DoesNotContain(privateConversationsList, c => c.TextContent.Contains("Friend")); // Friend ne match pas "alice"
    }

    [Fact]
    public async Task Chat_FilterButton_ShouldBeVisible()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general"))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = await RenderChatAsync(channelName: "general");

        // Assert
        Assert.Contains("filter-toggle-btn", cut.Markup);
    }
}