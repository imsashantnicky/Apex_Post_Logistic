namespace ApexPost.Web.ViewModels;

/// <summary>A void or refund form: the action to post to and its button text.</summary>
public sealed record ReversalForm(string Action, string Title, Guid TransactionId)
{
    public string ReasonId => $"{Action}Reason";
}
