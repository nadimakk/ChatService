using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record SendMessageResponse
{
    [Required] public long CreatedUnixTime { get; set; }
}