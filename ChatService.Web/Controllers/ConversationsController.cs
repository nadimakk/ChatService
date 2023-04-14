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
        int limit = 10, OrderBy orderBy = OrderBy.DESC, string? continuationToken = null, long lastSeenConversationTime = 0){

        using (_logger.BeginScope("{Username}", username))
        {
            try
            {
                GetUserConversationsServiceResult result = await _userConversationService.GetUserConversations(
                    username, limit, orderBy, continuationToken, lastSeenConversationTime);

                _logger.LogInformation("Fetched conversations of user {Username}", username);

                string nextUri = "";
                if (result.NextContinuationToken != null)
                {
                    nextUri = "/api/conversations" +
                              $"?username={username}" +
                              $"&limit={limit}" +
                              $"&lastSeenConversationTime={lastSeenConversationTime}" +
                              $"&continuationToken={result.NextContinuationToken}";
                }

                GetUserConversationsResponse response = new GetUserConversationsResponse
                {
                    Conversations = result.Conversations,
                    NextUri = nextUri
                };

                return Ok(response);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidContinuationTokenException)
            {
                _logger.LogError(e, "Error getting user conversations: {ErrorMessage}", e.Message);
                return BadRequest(e.Message);
            }
            catch (UserNotFoundException e)
            {
                _logger.LogError(e, "Error getting user conversations: {ErrorMessage}", e.Message);
                return NotFound(e.Message);
            }
        }
    }

    [HttpPost]
    public async Task<ActionResult<StartConversationResponse>> StartConversation(StartConversationRequest request)
    {
        using (_logger.BeginScope("{SenderUsername}", request.FirstMessage.SenderUsername))
        {
            try
            {
                StartConversationServiceResult result = await _userConversationService.CreateConversation(request);
                
                _logger.LogInformation(
                    "Created user conversation with Id {ConversationId} for user {Username}",
                    result.ConversationId, request.FirstMessage.SenderUsername);
                
                StartConversationResponse response = new StartConversationResponse
                {
                    ConversationId = result.ConversationId,
                    CreatedUnixTime = result.CreatedUnixTime
                };
                
                return CreatedAtAction(nameof(GetUserConversations), 
                    new { username = request.FirstMessage.SenderUsername }, response);
            }
            catch (ArgumentException e)
            {
                _logger.LogError(e, "Error creating user conversation: {ErrorMessage}", e.Message);
                return BadRequest(e.Message);
            }
            catch (ProfileNotFoundException e)
            {
                _logger.LogError(e, "Error creating user conversation: {ErrorMessage}", e.Message);
                return NotFound(e.Message);
            }
            catch (UserNotParticipantException e)
            {
                _logger.LogError(e, "Error creating user conversation: {ErrorMessage}", e.Message);
                return new ObjectResult(e.Message) { StatusCode = 403 };
            }
            catch (Exception e) when (e is MessageExistsException || e is UserConversationExistsException)
            {
                _logger.LogError(e, "Error creating user conversation: {ErrorMessage}", e.Message);
                return Conflict(e.Message);
            }
        }
    }

    [HttpGet("{conversationId}/messages")]
    public async Task<ActionResult<GetMessagesResponse>> GetMessages(string conversationId,
        int limit = 10, OrderBy orderBy = OrderBy.DESC, string? continuationToken = null, long lastSeenConversationTime = 0)
    {
        using (_logger.BeginScope("{ConversationId}", conversationId))
        {
            try
            {
                GetMessagesServiceResult result = await _messageService.GetMessages(
                    conversationId, limit, orderBy, continuationToken, lastSeenConversationTime);
            
                _logger.LogInformation("Fetched messages from conversation {ConversationId}", conversationId);

                string nextUri = "";
                if (result.NextContinuationToken != null)
                { 
                    nextUri = $"/api/conversations/{conversationId}/messages" +
                            $"&limit={limit}" +
                            $"&continuationToken={result.NextContinuationToken}" +
                            $"&lastSeenConversationTime={lastSeenConversationTime}";
                }

                GetMessagesResponse response = new GetMessagesResponse
                {
                    Messages = result.Messages,
                    NextUri = nextUri
                };
        
                return Ok(response);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidContinuationTokenException)
            {
                _logger.LogError(e, "Error getting messages: {ErrorMessage}", e.Message);
                return BadRequest(e.Message);
            }
            catch (ConversationDoesNotExistException e)
            {
                _logger.LogError(e, "Error getting messages: {ErrorMessage}", e.Message);
                return NotFound(e.Message);
            }   
        }
    }

    [HttpPost("{conversationId}/messages")]
    public async Task<ActionResult<SendMessageResponse>> PostMessage(string conversationId, SendMessageRequest request)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   {"ConversationId", conversationId},
                   {"SenderUsername", request.SenderUsername}
               }))
        {
            try
            {
                SendMessageResponse response = await _messageService.AddMessage(conversationId, false, request);
            
                _logger.LogInformation("Adding message {MessageId} to conversation {ConversationId} by sender {SenderUsername}",
                    request.MessageId, conversationId, request.SenderUsername);
                
                return CreatedAtAction(nameof(GetMessages), new { conversationId = conversationId}, response);
            }
            catch (ArgumentException e)
            {
                _logger.LogError(e, "Error adding message: {ErrorMessage}", e.Message);
                return BadRequest(e.Message);
            }
            catch (UserNotParticipantException e)
            {
                _logger.LogError(e, "Error adding message: {ErrorMessage}", e.Message);
                return new ObjectResult(e.Message) { StatusCode = 403 };
            }
            catch (Exception e) when (e is ProfileNotFoundException || e is ConversationDoesNotExistException)
            {
                _logger.LogError(e, "Error adding message: {ErrorMessage}", e.Message);
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