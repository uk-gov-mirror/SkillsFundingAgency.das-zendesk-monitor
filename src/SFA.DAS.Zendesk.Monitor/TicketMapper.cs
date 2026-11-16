using System;
using System.Linq;
using MiddlewareModels = SFA.DAS.Zendesk.Monitor.Middleware.Model;
using ZendeskModels = SFA.DAS.Zendesk.Monitor.Zendesk.Model;

namespace SFA.DAS.Zendesk.Monitor
{
    public static class TicketMapper
    {
        public static Middleware.EventWrapper Map(ZendeskModels.TicketResponse response)
        {
            _ = response ?? throw new ArgumentNullException(nameof(response));

            var ticket = MapTicket(response.Ticket);

            ticket.Comments = MapArray(response.Comments, MapComment);
            ticket.Requester = MapUser(response.Users?.FirstOrDefault(x => x.Id == response.Ticket.RequesterId));
            ticket.Organization = MapOrganisation(response.Organizations?.FirstOrDefault(x => x.Id == response.Ticket.OrganizationId));

            return new Middleware.EventWrapper { Ticket = ticket };
        }

        public static MiddlewareModels.Ticket MapTicket(ZendeskModels.Ticket t)
        {
            _ = t ?? throw new ArgumentNullException(nameof(t));

            return new MiddlewareModels.Ticket
            {
                Id = t.Id,
                Status = t.Status,
                Description = t.Description,
                CreatedAt = t.CreatedAt ?? default,
                Subject = t.Subject,
                Via = TranslateVia(t.Via),
                Destination = FindDestination(t),
                CustomFields = (t.CustomFields ?? Array.Empty<ZendeskModels.Field>())
                    .Where(f => f.Value != null)
                    .Select(f => new MiddlewareModels.CustomField { Id = f.Id, Value = f.Value.ToString() })
                    .ToArray(),
            };
        }

        private static MiddlewareModels.Comments MapComment(ZendeskModels.Comment c) => new MiddlewareModels.Comments
        {
            Id = c.Id ?? 0,
            Body = c.Body,
            CreatedAt = c.CreatedAt ?? default,
            Attachments = MapArray(c.Attachments, MapAttachment),
        };

        private static MiddlewareModels.Attachment MapAttachment(ZendeskModels.Attachment a) => new MiddlewareModels.Attachment
        {
            Filename = a.FileName,
            Url = a.ContentUrl,
        };

        private static MiddlewareModels.User? MapUser(ZendeskModels.User? u)
        {
            if (u == null)
            {
                return null;
            }

            return new MiddlewareModels.User
            {
                Id = u.Id ?? 0,
                Name = u.Name,
                Email = u.Email ?? "",
                Phone = Str(u.Phone),
                UserFields = MapUserFields(u.UserFields),
            };
        }

        private static MiddlewareModels.UserFields? MapUserFields(ZendeskModels.UserFields? f)
        {
            if (f == null)
            {
                return null;
            }

            return new MiddlewareModels.UserFields
            {
                AddressLine1 = Str(f.AddressLine1),
                AddressLine2 = Str(f.AddressLine2),
                AddressLine3 = Str(f.AddressLine3),
                City = Str(f.City),
                County = Str(f.County),
                Postcode = Str(f.Postcode),
                ContactType = Str(f.ContactType),
            };
        }

        private static MiddlewareModels.Organisation? MapOrganisation(ZendeskModels.Organization? o)
        {
            if (o == null)
            {
                return null;
            }

            return new MiddlewareModels.Organisation
            {
                Id = o.Id ?? 0,
                Name = o.Name,
                OrganizationFields = MapOrganisationFields(o.OrganizationFields),
            };
        }

        private static MiddlewareModels.OrganizationFields? MapOrganisationFields(ZendeskModels.OrganizationFields? f)
        {
            if (f == null)
            {
                return null;
            }

            return new MiddlewareModels.OrganizationFields
            {
                AddressLine1 = Str(f.AddressLine1),
                AddressLine2 = Str(f.AddressLine2),
                AddressLine3 = Str(f.AddressLine3),
                City = Str(f.City),
                County = Str(f.County),
                Postcode = Str(f.Postcode),
                OrganisationType = Str(f.OrganisationType),
                MainPhone = Str(f.MainPhone),
                OrganisationStatus = Str(f.OrganisationStatus),
            };
        }

        private static string FindDestination(ZendeskModels.Ticket t) =>
            t.Tags
                ?.Find(x => x.StartsWith("middleware_destination_"))
                ?.Split("_").Last()
            ?? "itsm";

        private static string TranslateVia(ZendeskModels.Via? via) =>
            (via?.Channel, via?.Source?.Rel) switch
            {
                ("voice", "voicemail") => "Voicemail",
                ("voice", _) => $"Phone call ({via?.Source?.Rel})",
                ("email", _) => "Mail",
                ("twitter", _) => "Mail",
                ("api", _) => "Mail",
                ("chat", _) => "Chat",
                ("web", _) => "Web Form",
                _ => via?.Channel?.ToLower(),
            } ?? "";

        private static string Str(object? value) => value?.ToString() ?? "";

        private static TDest[] MapArray<TSrc, TDest>(TSrc[]? source, Func<TSrc, TDest> map)
            => source?.Select(map).ToArray() ?? Array.Empty<TDest>();
    }
}