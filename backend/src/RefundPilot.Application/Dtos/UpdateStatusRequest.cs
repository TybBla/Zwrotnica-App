using System.ComponentModel.DataAnnotations;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record UpdateStatusRequest([Required] CaseStatus? Status);
