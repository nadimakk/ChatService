using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IUserConversationService _userConversationService;
    private readonly IMessageService _messageService;
    private readonly ILogger<ConversationsController> _logger;


    public ConversationsController(
        IUserConversationService userConversationService,
        IMessageService messageService,
        ILogger<ConversationsController> logger
        )
    {
        _userConversationService = userConversationService;
        _messageService = messageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<GetUserConversationsResponse>> GetUserConversations(string username, 
        int limit = 10, OrderBy orderBy = OrderBy.DESC, string? continuationToken = null, long lastSeenConversationTime = 0)
    {
        try
        {
            GetUserConversationsParameters parameters = new()
            {
                Limit = limit,
                Order = orderBy,
                ContinuationToken = continuationToken,
                LastSeenConversationTime = lastSeenConversationTime
            };
            GetConversationsResult result = await _userConversationService.GetUserConversations(username, parameters);

            string nextUri = "";
            if (result.NextContinuationToken != null)
            {
                nextUri = "/api/conversations" +
                          $"?username={username}" +
                          $"&limit={limit}" +
                          $"&lastSeenConversationTime={lastSeenConversationTime}" +
                          $"&continuationToken={WebUtility.UrlEncode(result.NextContinuationToken)}";
            }

            GetUserConversationsResponse response = new()
            {
                Conversations = result.Conversations,
                NextUri = nextUri
            };

            return Ok(response);
        }
        catch (Exception e) when (e is ArgumentException or InvalidContinuationTokenException)
        {
            return BadRequest(e.Message);
        }
        catch (UserNotFoundException e)
        {
            return NotFound(e.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<StartConversationResponse>> StartConversation(StartConversationRequest request)
    {
        using (_logger.BeginScope("{Username}", request.FirstMessage.SenderUsername))
        {
            try
            {
                StartConversationResult result = await _userConversationService.CreateConversation(request);
                
                _logger.LogInformation(
                    "Created user conversation with Id {ConversationId} for user {Username}",
                    result.ConversationId, request.FirstMessage.SenderUsername);
                
                StartConversationResponse response = new()
                {
                    Id = result.ConversationId,
                    CreatedUnixTime = result.CreatedUnixTime
                };
                
                return CreatedAtAction(nameof(GetUserConversations), 
                    new { username = request.FirstMessage.SenderUsername }, response);
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }
            catch (UserNotFoundException e)
            {
                return NotFound(e.Message);
            }
            catch (UserNotParticipantException e)
            {
                _logger.LogError(e, "Error creating user conversation: {ErrorMessage}", e.Message);
                return new ObjectResult(e.Message) { StatusCode = 403 };
            }
            catch (Exception e) when (e is MessageExistsException or UserConversationExistsException)
            {
                _logger.LogError(e, "Error creating user conversation: {ErrorMessage}", e.Message);
                return Conflict(e.Message);
            }
        }
    }

    [HttpGet("{conversationId}/messages")]
    public async Task<ActionResult<GetMessagesResponse>> GetMessages(string conversationId,
        int limit = 50, OrderBy orderBy = OrderBy.DESC, string? continuationToken = null, long lastSeenMessageTime = 0)
    {
        try
        {
            GetMessagesParameters parameters = new()
            {
                Limit = limit,
                Order = orderBy,
                ContinuationToken = continuationToken,
                LastSeenMessageTime = lastSeenMessageTime
            };
            GetMessagesResult result = await _messageService.GetMessages(conversationId, parameters);

            string nextUri = "";
            if (result.NextContinuationToken != null)
            { 
                nextUri = $"/api/conversations/{conversationId}/messages" +
                        $"?limit={limit}" +
                        $"&continuationToken={WebUtility.UrlEncode(result.NextContinuationToken)}" +
                        $"&lastSeenConversationTime={lastSeenMessageTime}";
            }
            
            GetMessagesResponse response = new()
            {
                Messages = result.Messages,
                NextUri = nextUri
            };
            return Ok(response);
        }
        catch (Exception e) when (e is ArgumentException or InvalidContinuationTokenException)
        {
            return BadRequest(e.Message);
        }
        catch (ConversationDoesNotExistException e)
        {
            return NotFound(e.Message);
        }
    }

    [HttpPost("{conversationId}/messages")]
    public async Task<ActionResult<SendMessageResponse>> PostMessage(string conversationId, SendMessageRequest request)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   {"ConversationId", conversationId},
                   {"Username", request.SenderUsername}
               }))
        {
            try
            {
                SendMessageResponse response = await _messageService.AddMessage(conversationId, isFirstMessage: false, request);
            
                _logger.LogInformation("Adding message {MessageId} to conversation {ConversationId} by sender {SenderUsername}",
                    request.Id, conversationId, request.SenderUsername);
                
                return CreatedAtAction(nameof(GetMessages), new { conversationId = conversationId }, response);
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }
            catch (UserNotParticipantException e)
            {
                _logger.LogError(e, "Error adding message: {ErrorMessage}", e.Message);
                return new ObjectResult(e.Message) { StatusCode = 403 };
            }
            catch (Exception e) when (e is UserNotFoundException or ConversationDoesNotExistException)
            {
                return NotFound(e.Message);
            }
            catch (MessageExistsException e)
            {
                _logger.LogError(e, "Error adding message: {ErrorMessage}", e.Message);
                return Conflict(e.Message);
            }
        }
    }
}