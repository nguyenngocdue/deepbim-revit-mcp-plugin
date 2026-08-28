using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Commands.Driver
{
    /// <summary>rcd_dialog_policy — set / clear / list session rules for auto-answering Revit dialogs.</summary>
    public class RcdDialogPolicyCommand : IRevitCommand
    {
        public string CommandName => "rcd_dialog_policy";

        public RcdDialogPolicyCommand(UIApplication uiApp) { }

        public object Execute(JObject parameters, string requestId)
        {
            try
            {
                string action = (parameters["action"]?.ToString() ?? "list").ToLowerInvariant();
                switch (action)
                {
                    case "set":
                        {
                            var rules = parameters["rules"]?.ToObject<List<DialogRule>>() ?? new List<DialogRule>();
                            if (rules.Count == 0) throw new DriverException(RcdErrorCodes.InvalidArgument, "rules[] is required for action=set.");
                            foreach (var r in rules)
                                if (string.IsNullOrEmpty(r.DialogId) && string.IsNullOrEmpty(r.MessageRegex) && string.IsNullOrEmpty(r.TitleRegex))
                                    throw new DriverException(RcdErrorCodes.InvalidArgument, "Each rule needs dialogId, messageRegex or titleRegex.");
                            int ttl = parameters["ttlMs"]?.Value<int>() ?? 60000;
                            DialogPolicy.SetRules(rules, ttl);
                            break;
                        }
                    case "clear":
                        DialogPolicy.ClearRules();
                        break;
                    case "list":
                        break;
                    default:
                        throw new DriverException(RcdErrorCodes.InvalidArgument, "action must be set | clear | list.");
                }
                return RcdRuntime.Ok(new
                {
                    rules = DialogPolicy.ListRules(),
                    recentEvents = DialogPolicy.RecentEvents(30),
                    hooksInstalled = RcdRuntime.HooksInstalled,
                    note = RcdRuntime.HooksInstalled ? null : "Dialog hook not installed yet — it is installed by the first rcd_list_commands / rcd_post_command call."
                });
            }
            catch (Exception ex)
            {
                return RcdRuntime.FailFromException(ex);
            }
        }
    }
}
