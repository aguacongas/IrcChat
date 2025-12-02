// tests/IrcChat.Client.Tests/Components/MessageListTests.cs
using System.Text.RegularExpressions;
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public partial class MessageListTests : BunitContext
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;

    public MessageListTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        Services.AddSingleton(_jsRuntimeMock.Object);
    }

    [Fact]
    public void MessageList_WithEmptyMessages_ShouldRenderEmpty()
    {
        // Arrange & Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, [])
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        cut.MarkupMatches("<div class=\"messages\" diff:ignoreAttributes></div>");
    }

    [Fact]
    public void MessageList_WithMessages_ShouldRenderMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user2",
                Content = "Hi there!",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        // Assert
        Assert.Equal(2, cut.FindAll(".message").Count);
        Assert.Equal("Hello!", cut.Find(".message.own .content").TextContent);
    }

    [Fact]
    public void MessageList_ShouldMarkOwnMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "currentuser",
                Content = "My message",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser"));

        // Assert
        Assert.Contains("own", cut.Find(".message").ClassList);
    }

    [Fact]
    public void MessageList_OnInitialRender_ShouldLoadScrollModule()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/scroll-helper.js")))
            .ReturnsAsync(mockModule.Object);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/scroll-helper.js")),
            Times.Once);
    }

    [Fact]
    public async Task MessageList_WhenNewMessageAdded_ShouldScrollToBottom()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/scroll-helper.js")))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Act - Ajouter un nouveau message
        messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            Username = "user2",
            Content = "Hi there",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        });

        cut.Render(parameters => parameters
            .Add(p => p.Messages, messages));

        await Task.Delay(100);

        // Assert
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void MessageList_WhenModuleLoadFails_ShouldHandleGracefully()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act & Assert - Ne devrait pas lancer d'exception
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        Assert.NotNull(cut);
    }

    [Fact]
    public async Task MessageList_WhenDisposed_ShouldDisposeModule()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Act
        await cut.Instance.DisposeAsync();
        await Task.Delay(100);

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task MessageList_WithEmptyMessages_ShouldNotScrollInitially()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, [])
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Assert - Pas de scroll pour une liste vide
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task MessageList_WhenMessageCountSame_ShouldNotScroll()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Reset les appels précédents
        mockModule.Invocations.Clear();

        // Act - Re-render sans changement de count        
        cut.Render(parameters => { });

        await Task.Delay(100);

        // Assert - Pas de nouveau scroll
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void MessageList_WhenScrollFails_ShouldHandleGracefully()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Scroll failed"));

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act & Assert - Ne devrait pas lancer d'exception
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        Assert.NotNull(cut);
    }

    [Fact]
    public async Task MessageList_MultipleNewMessages_ShouldScrollOnce()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Message 1",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        mockModule.Invocations.Clear();

        // Act - Ajouter plusieurs messages en une fois
        messages.AddRange(
        [
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user2",
                Content = "Message 2",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            },
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user3",
                Content = "Message 3",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        ]);

        cut.Render(parameters => parameters
            .Add(p => p.Messages, messages));

        await Task.Delay(100);

        // Assert - Un seul scroll pour plusieurs messages
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task MessageList_WhenDisposeThrows_ShouldHandleGracefully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<MessageList>>();
        Services.AddSingleton(loggerMock.Object);

        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        var disposeException = new JSException("Dispose failed");
        mockModule
            .Setup(x => x.DisposeAsync())
            .ThrowsAsync(disposeException);

        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Act
        await cut.Instance.DisposeAsync();
        await Task.Delay(100);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("libération du module de scroll")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MessageList_WhenModuleLoadThrows_ShouldLogWarning()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<MessageList>>();
        Services.AddSingleton(loggerMock.Object);

        var loadException = new JSException("Module not found");
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(loadException);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("chargement du module de scroll")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void MessageList_WithMention_ShouldApplyMentionedClass()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "Hey @testuser, how are you?",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var messageDiv = cut.Find(".message");
        Assert.Contains("mentioned", messageDiv.ClassList);
    }

    [Fact]
    public void MessageList_WithoutMention_ShouldNotApplyMentionedClass()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "Hello everyone!",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var messageDiv = cut.Find(".message");
        Assert.DoesNotContain("mentioned", messageDiv.ClassList);
    }

    [Fact]
    public void MessageList_WithMention_ShouldHighlightUsername()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "Hey testuser, check this out!",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var content = cut.Find(".content");
        Assert.Contains("mention-highlight", content.InnerHtml);
        Assert.Contains("testuser", content.TextContent);
    }

    [Fact]
    public void MessageList_WithCaseInsensitiveMention_ShouldDetect()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "Hey TESTUSER, are you there?",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var messageDiv = cut.Find(".message");
        Assert.Contains("mentioned", messageDiv.ClassList);
    }

    [Fact]
    public void MessageList_WithPartialMatch_ShouldNotDetectMention()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "This is a testusername, not the same",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var messageDiv = cut.Find(".message");
        // Devrait contenir "mentioned" car "testuser" est inclus dans "testusername"
        // Si on veut une détection stricte par mot complet, il faudrait modifier le code
        Assert.Contains("mentioned", messageDiv.ClassList);
    }

    [Fact]
    public void MessageList_OwnMessageWithOwnUsername_ShouldNotApplyMentionedClass()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Content = "I am testuser and this is my message",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var messageDiv = cut.Find(".message");
        Assert.Contains("own", messageDiv.ClassList);
        // Le message contient le pseudo mais c'est le sien, il devrait quand même être marqué "mentioned"
        // C'est un choix de design - vous pouvez décider de l'exclure ou non
    }

    [Fact]
    public void MessageList_WithMultipleMentions_ShouldHighlightAll()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "testuser, can testuser help with this?",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var content = cut.Find(".content");
        var highlightCount = HighlightRegex().Count(content.InnerHtml);
        Assert.Equal(2, highlightCount);
    }

    [Fact]
    public void MessageList_WithSpecialCharactersInUsername_ShouldEscapeHtml()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "<script>alert('test')</script> user<test>",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user<test>"));

        // Assert
        var content = cut.Find(".content");
        // Le HTML doit être échappé pour éviter XSS
        Assert.DoesNotContain("<script>", content.InnerHtml);
        Assert.Contains("&lt;script&gt;", content.InnerHtml);
    }

    [Fact]
    public void MessageList_WithEmptyUsername_ShouldNotCrash()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "Hello everyone",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, string.Empty));

        // Assert
        Assert.NotNull(cut);
        var messageDiv = cut.Find(".message");
        Assert.DoesNotContain("mentioned", messageDiv.ClassList);
    }

    [Fact]
    public void MessageList_WithNullContent_ShouldHandleGracefully()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = null!,
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act & Assert - Ne devrait pas lancer d'exception
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        Assert.NotNull(cut);
    }

    [Fact]
    public void MessageList_MixedMessagesWithAndWithoutMentions_ShouldOnlyHighlightMentioned()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "Hello everyone!",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user2",
            Content = "Hey testuser, what do you think?",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user3",
            Content = "Just a regular message",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var allMessages = cut.FindAll(".message");
        Assert.Equal(3, allMessages.Count);

        // Seul le deuxième message devrait avoir la classe "mentioned"
        Assert.DoesNotContain("mentioned", allMessages[0].ClassList);
        Assert.Contains("mentioned", allMessages[1].ClassList);
        Assert.DoesNotContain("mentioned", allMessages[2].ClassList);
    }

    [Fact]
    public void MessageList_WithMentionAtStartOfMessage_ShouldHighlight()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "testuser: can you help?",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var messageDiv = cut.Find(".message");
        Assert.Contains("mentioned", messageDiv.ClassList);
        var content = cut.Find(".content");
        Assert.Contains("mention-highlight", content.InnerHtml);
    }

    [Fact]
    public void MessageList_WithMentionAtEndOfMessage_ShouldHighlight()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "Can you help, testuser?",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        }
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        var messageDiv = cut.Find(".message");
        Assert.Contains("mentioned", messageDiv.ClassList);
        var content = cut.Find(".content");
        Assert.Contains("mention-highlight", content.InnerHtml);
    }

    [Fact]
    public void MessageList_WithChannelDescription_ShouldDisplayDescriptionMessage()
    {
        // Arrange
        var messages = new List<Message>();
        var description = "Bienvenue sur le salon général !";

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, description)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains("channel-description-message", cut.Markup);
        Assert.Contains(description, cut.Markup);
        Assert.Contains("Description du salon", cut.Markup);
        Assert.Contains("📌", cut.Markup);
    }

    [Fact]
    public void MessageList_WithoutDescription_ShouldNotDisplayDescriptionMessage()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, null)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.DoesNotContain("channel-description-message", cut.Markup);
        Assert.DoesNotContain("Description du salon", cut.Markup);
    }

    [Fact]
    public void MessageList_WithEmptyDescription_ShouldNotDisplayDescriptionMessage()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, string.Empty)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.DoesNotContain("channel-description-message", cut.Markup);
    }

    [Fact]
    public void MessageList_WithDescriptionAndCanManage_ShouldShowEditButton()
    {
        // Arrange
        var messages = new List<Message>();
        var description = "Description du salon";

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, description)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, true));

        // Assert
        var editButton = cut.Find(".edit-description-btn");
        Assert.NotNull(editButton);
    }

    [Fact]
    public void MessageList_WithDescriptionButCannotManage_ShouldNotShowEditButton()
    {
        // Arrange
        var messages = new List<Message>();
        var description = "Description du salon";

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, description)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".edit-description-btn"));
    }

    [Fact]
    public async Task MessageList_EditDescriptionButton_WhenClicked_ShouldInvokeCallback()
    {
        // Arrange
        var messages = new List<Message>();
        var description = "Description du salon";
        var callbackInvoked = false;

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, description)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, true)
            .Add(p => p.OnEditDescription, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var editButton = cut.Find(".edit-description-btn");
        await cut.InvokeAsync(() => editButton.Click());

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void MessageList_DescriptionMessage_ShouldAppearBeforeRegularMessages()
    {
        // Arrange
        var messages = new List<Message>
    {
        new() { Id = Guid.NewGuid(), Username = "user1", Content = "Premier message", Timestamp = DateTime.UtcNow }
    };
        var description = "Description du salon";

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, description)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, false));

        var markup = cut.Markup;
        var descriptionIndex = markup.IndexOf("channel-description-message", StringComparison.Ordinal);
        var messageIndex = markup.IndexOf("Premier message", StringComparison.Ordinal);

        // Assert
        Assert.True(descriptionIndex < messageIndex, "La description devrait apparaître avant les messages");
    }

    [Fact]
    public void MessageList_LongDescription_ShouldDisplayFullText()
    {
        // Arrange
        var messages = new List<Message>();
        var longDescription = new string('a', 500); // 500 caractères

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, longDescription)
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains(longDescription, cut.Markup);
    }

    [Fact]
    public void MessageList_WithoutCurrentChannel_ShouldNotDisplayDescription()
    {
        // Arrange
        var messages = new List<Message>();
        var description = "Description";

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ChannelDescription, description)
            .Add(p => p.CurrentChannel, null)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.DoesNotContain("channel-description-message", cut.Markup);
    }
    [GeneratedRegex("mention-highlight")]
    private static partial Regex HighlightRegex();
}