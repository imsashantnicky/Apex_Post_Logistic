using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ApexPost.Api.Controllers;

/// <summary>
/// Removes the Development-only controllers (<see cref="DevTokenController"/>) from the application outside
/// Development. They get no route at all, which is stronger than an action that checks the environment.
/// </summary>
public sealed class DevelopmentOnlyControllers : IApplicationFeatureProvider<ControllerFeature>
{
    /// <summary>The controllers that exist in Development only.</summary>
    public static readonly IReadOnlyList<TypeInfo> Controllers = [typeof(DevTokenController).GetTypeInfo()];

    /// <summary>Takes the Development-only controllers out of the discovered controllers.</summary>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        foreach (var controller in Controllers)
        {
            feature.Controllers.Remove(controller);
        }
    }
}
