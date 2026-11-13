using System.ComponentModel.DataAnnotations;

namespace RefundPilot.Application.Dtos;

/// <summary>The (possibly user-edited) letter to render as PDF; no AI call and no data is written.</summary>
public sealed record ComplaintPdfRequest(
    [Required(ErrorMessage = "Podaj temat pisma.")]
    [MaxLength(200, ErrorMessage = "Temat może mieć maksymalnie 200 znaków.")] string Subject,
    [Required(ErrorMessage = "Podaj treść pisma.")]
    [MaxLength(6000, ErrorMessage = "Treść może mieć maksymalnie 6000 znaków.")] string Body);
