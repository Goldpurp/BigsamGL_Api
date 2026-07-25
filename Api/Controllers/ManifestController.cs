using Application.Features.CargoItems.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.Net;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "OperationsTeam")]
public class ManifestController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IClientCommunicationService _communications;
    public ManifestController(IApplicationDbContext context, IClientCommunicationService communications)
    {
        _context = context; _communications = communications;
    }
    [HttpGet("{batchId}/pdf")]
    public async Task<IActionResult> GetManifestPdfPreview(Guid batchId)
    {
        var query = new GetManifestQuery(batchId);
        var manifest = await Mediator.Send(query);

        var htmlBuilder = new StringBuilder();
        static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        htmlBuilder.Append($@"
<!DOCTYPE html>
<html>
<head>
    <title>Consolidated Cargo Manifest - {H(manifest.BatchCode)}</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; color: #333; }}
        .header {{ border-bottom: 2px solid #2B6CB0; padding-bottom: 20px; margin-bottom: 30px; }}
        .header h1 {{ margin: 0; color: #2B6CB0; font-size: 24px; text-transform: uppercase; }}
        .header p {{ margin: 5px 0 0 0; color: #666; font-size: 14px; }}
        .meta-grid {{ display: grid; grid-template-columns: repeat(2, 1fr); gap: 15px; margin-bottom: 30px; background-color: #F7FAFC; padding: 20px; border-radius: 8px; border: 1px solid #E2E8F0; }}
        .meta-item {{ font-size: 14px; }}
        .meta-item strong {{ color: #4A5568; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
        th, td {{ border: 1px solid #E2E8F0; padding: 12px 15px; text-align: left; font-size: 14px; }}
        th {{ background-color: #EDF2F7; color: #2D3748; font-weight: 600; text-transform: uppercase; font-size: 12px; }}
        tr:nth-child(even) td {{ background-color: #F8FAFC; }}
        .summary {{ margin-top: 30px; text-align: right; font-size: 14px; color: #4A5568; font-weight: bold; border-top: 2px solid #E2E8F0; padding-top: 15px; }}
        .footer-notice {{ margin-top: 50px; text-align: center; font-size: 11px; color: #A0AEC0; border-top: 1px solid #E2E8F0; padding-top: 20px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>BiGSAM Global Logistics</h1>
        <p>Freight Forwarding & Consolidation Manifest</p>
    </div>
    
    <div class='meta-grid'>
        <div class='meta-item'><strong>Freight Batch Identifier:</strong> {H(manifest.BatchCode)}</div>
        <div class='meta-item'><strong>BiGSAM Role:</strong> Freight Forwarder &amp; Consolidator</div>
        <div class='meta-item'><strong>External Carrier / Partner:</strong> {H(manifest.Transport.CarrierName ?? "Pending")}</div>
        <div class='meta-item'><strong>Carrier Reference:</strong> {H(manifest.Transport.MasterTransportReference ?? "Pending")}</div>
        <div class='meta-item'><strong>Flight / Vessel:</strong> {H(manifest.Transport.FlightNumber ?? manifest.Transport.VesselName ?? "Pending")}</div>
        <div class='meta-item'><strong>Voyage Number:</strong> {H(manifest.Transport.VoyageNumber ?? "N/A")}</div>
        <div class='meta-item'><strong>Origin Gateway:</strong> {H(manifest.Transport.OriginGateway ?? manifest.PortOfLoading ?? "N/A")}</div>
        <div class='meta-item'><strong>Destination Gateway:</strong> {H(manifest.Transport.DestinationGateway ?? manifest.PortOfDischarge ?? "N/A")}</div>
        <div class='meta-item'><strong>Scheduled Release Date:</strong> {manifest.ScheduledReleaseDate:yyyy-MM-dd HH:mm} UTC</div>
    </div>

    <h2>Consolidated Cargo Manifest Items</h2>
    <table>
        <thead>
            <tr>
                <th>Item Marker Code (SGMC)</th>
                <th>Route details</th>
                <th>Pieces</th>
                <th>Verified Weight (Kg)</th>
                <th>Volume (Cbm)</th>
            </tr>
        </thead>
        <tbody>");

        decimal totalWeight = 0;
        decimal totalVolume = 0;
        int totalPieces = 0;

        foreach (var item in manifest.LineItems)
        {
            totalWeight += item.VerifiedWeightKg;
            totalVolume += item.VolumeCbm;
            totalPieces += item.PieceCount;

            htmlBuilder.Append($@"
            <tr>
                <td><strong>{H(item.MarkerCode)}</strong></td>
                <td>{H(item.RouteDetails)}</td>
                <td>{item.PieceCount}</td>
                <td>{item.VerifiedWeightKg:F2} kg</td>
                <td>{item.VolumeCbm:F3} m³</td>
            </tr>");
        }

        htmlBuilder.Append($@"
        </tbody>
    </table>

    <div class='summary'>
        Total Pieces: {totalPieces} | Total Gross Weight: {totalWeight:F2} kg | Total Cubic Volume: {totalVolume:F3} m³
    </div>

    <div class='footer-notice'>
        Prepared by BiGSAM Global Logistics from its forwarding and consolidation records. External carrier acceptance,
        transport references, and customs clearance remain subject to confirmation by the responsible carrier and authorities.
        Internal financial balances and billing data are intentionally excluded.
    </div>
</body>
</html>");

        return Content(htmlBuilder.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("{batchId}/excel")]
    public async Task<IActionResult> GetManifestCsvDownload(Guid batchId)
    {
        var query = new GetManifestQuery(batchId);
        var manifest = await Mediator.Send(query);

        static string Csv(string? value)
        {
            var safe = value ?? string.Empty;
            if (safe.Length > 0 && "=+-@".Contains(safe[0])) safe = "'" + safe;
            return $"\"{safe.Replace("\"", "\"\"")}\"";
        }

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("BiGSAM Freight Forwarding and Consolidation Manifest");
        csvBuilder.AppendLine("Company,BiGSAM Global Logistics");
        csvBuilder.AppendLine("BiGSAM Role,Freight Forwarder and Consolidator");
        csvBuilder.AppendLine($"Batch Code,{Csv(manifest.BatchCode)}");
        csvBuilder.AppendLine($"External Carrier or Partner,{Csv(manifest.Transport.CarrierName)}");
        csvBuilder.AppendLine($"Carrier Reference,{Csv(manifest.Transport.MasterTransportReference)}");
        csvBuilder.AppendLine($"Flight,{Csv(manifest.Transport.FlightNumber)}");
        csvBuilder.AppendLine($"Vessel,{Csv(manifest.Transport.VesselName)}");
        csvBuilder.AppendLine($"Voyage,{Csv(manifest.Transport.VoyageNumber)}");
        csvBuilder.AppendLine($"Origin Gateway,{Csv(manifest.Transport.OriginGateway ?? manifest.PortOfLoading)}");
        csvBuilder.AppendLine($"Destination Gateway,{Csv(manifest.Transport.DestinationGateway ?? manifest.PortOfDischarge)}");
        csvBuilder.AppendLine($"Date,{manifest.ScheduledReleaseDate:yyyy-MM-dd}");
        csvBuilder.AppendLine();
        csvBuilder.AppendLine("Marker Code (SGMC),Route Details,Pieces,Verified Weight (Kg),Volume (Cbm)");

        foreach (var item in manifest.LineItems)
        {
            csvBuilder.AppendLine($"{Csv(item.MarkerCode)},{Csv(item.RouteDetails)},{item.PieceCount},{item.VerifiedWeightKg:F2},{item.VolumeCbm:F3}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        return File(csvBytes, "text/csv", $"Manifest_{manifest.BatchCode}.csv");
    }

    [HttpPost("{batchId:guid}/email")]
    public async Task<ActionResult> EmailManifest(Guid batchId, SendManifestEmailRequest request, CancellationToken cancellationToken)
    {
        var belongsToClient = await _context.CargoItems.AnyAsync(x => x.BatchId == batchId && x.CustomerId == request.CustomerId, cancellationToken);
        if (!belongsToClient) return BadRequest("This manifest does not contain cargo for the selected client.");
        var manifest = await Mediator.Send(new GetManifestQuery(batchId), cancellationToken);
        var clientItems = await _context.CargoItems.AsNoTracking().Where(x => x.BatchId == batchId && x.CustomerId == request.CustomerId)
            .Select(x => new { x.TrackingNumber, x.MarkerCode, x.Description, x.ActualWeightKg }).ToListAsync(cancellationToken);
        var message = await _communications.SendAsync(request.CustomerId, CommunicationType.Manifest, request.Subject,
            request.Body, "Manifest", batchId, request.AttachmentDocumentIds,
            new { manifest.BatchCode, manifest.AirwayBillNumber, manifest.VesselName, manifest.VoyageNumber,
                manifest.PortOfLoading, manifest.PortOfDischarge, manifest.ScheduledReleaseDate,
                manifest.Transport, ClientItems = clientItems }, cancellationToken);
        return Ok(new { message.Id, message.Status, manifest.BatchCode, Accepted = true });
    }
}
