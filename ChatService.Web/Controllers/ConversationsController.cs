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

    public ConversationsController(IUserConversationService userConversationService, IMessageService messageService)
    {
        _userConversationService = userConversationService;
        _messageService = messageService;
    }

    [HttpGet]
    public async Task<ActionResult<GetUserConversationsResponse>> GetUserConversations(string username, 
        int limit = 10, OrderBy orderBy = OrderBy.DESC, string? continuationToken = null, long lastSeenConversationTime = 0)
    {
        GetUserConversationsServiceResult result;

        try
        {
            result = await _userConversationService.GetUserConversations(
                username, limit, orderBy, continuationToken, lastSeenConversationTime);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (UserNotFoundException e)
        {
            return NotFound(e.Message);
        }
        
        string nextUri = "/api/conversations" +
                         $"?username={username}" +
                         $"&limit={limit}" +
                         $"&lastSeenConversationTime={lastSeenConversationTime}" +
                         $"&continuationToken={result.NextContinuationToken}";
        
        GetUserConversationsResponse response = new GetUserConversationsResponse
        {
            Conversations = result.Conversations,
            NextUri = nextUri
        };
        
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<StartConversationResponse>> StartConversation(StartConversationRequest request)
    {
        StartConversationResponse response;

        try
        {
            response = await _userConversationService.CreateConversation(request);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (ProfileNotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (MessageExistsException e)
        {
            return Conflict(e.Message);
        }
        
        return CreatedAtAction(nameof(GetUserConversations), 
            new { username = request.FirstMessage.SenderUsername }, response);
    }

    [HttpGet("{conversationId}/messages")]
    public async Task<ActionResult<GetMessagesResponse>> GetMessages(string conversationId,
        int limit = 10, OrderBy orderBy = OrderBy.DESC, string? continuationToken = null, long lastSeenConversationTime = 0)
    {
        GetMessagesServiceResult result;

        try
        {
            result = await _messageService.GetMessages(
                conversationId, limit, orderBy, continuationToken, lastSeenConversationTime);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (ConversationDoesNotExistException e)
        {
            return NotFound(e.Message);
        }
        
        string nextUri = $"/api/conversations/{conversationId}/messages" +
                         $"&limit={limit}" +
                         $"&continuationToken={result.NextContinuationToken}" +
                         $"&lastSeenConversationTime={lastSeenConversationTime}";
        
        GetMessagesResponse response = new GetMessagesResponse
        {
            Messages = result.Messages,
            NextUri = nextUri
        };
        
        return Ok(response);
    }

    [HttpPost("{conversationId}/messages")]
    public async Task<ActionResult<SendMessageResponse>> PostMessage(string conversationId, SendMessageRequest request)
    {
        SendMessageResponse response;

        try
        {
            response = await _messageService.AddMessage(conversationId, false, request);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (UserNotParticipantException e)
        {
            return new ObjectResult(e.Message) { StatusCode = 403 };
        }
        catch (Exception e) when (e is ProfileNotFoundException || e is ConversationDoesNotExistException)
        {
            return NotFound(e.Message);
        }
        catch (MessageExistsException e)
        {
            return Conflict(e.Message);
        }
        
        return CreatedAtAction(nameof(GetMessages), new { conversationId = conversationId}, response);
    }
}