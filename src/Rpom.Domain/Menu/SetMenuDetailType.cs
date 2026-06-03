namespace Rpom.Domain.Menu;

/// <summary>SetMenuDetail discriminator values.</summary>
public static class SetMenuDetailType
{
    /// <summary>Fixed/optional component dish in the SET_MENU.</summary>
    public const string Component = "COMPONENT";

    /// <summary>Modifier choice group attached to the SET_MENU.</summary>
    public const string ChoiceCategory = "CHOICE_CATEGORY";
}
