namespace EuroleagueQuiz.Api.Auditing;

public static class AuditActions
{
    public const string UserRegistered = "User.Registered";
    public const string LoginSucceeded = "Auth.LoginSucceeded";
    public const string LoginFailed = "Auth.LoginFailed";
    public const string Logout = "Auth.Logout";
    public const string GoogleLoginSucceeded = "Auth.GoogleLoginSucceeded";
    public const string GoogleLoginFailed = "Auth.GoogleLoginFailed";
    public const string SessionRefreshFailed = "Auth.SessionRefreshFailed";
    public const string EmailConfirmed = "Auth.EmailConfirmed";
    public const string EmailConfirmationFailed = "Auth.EmailConfirmationFailed";
    public const string ConfirmationRequested = "Auth.ConfirmationRequested";
    public const string PasswordResetRequested = "Auth.PasswordResetRequested";
    public const string PasswordSetupRequested = "Auth.PasswordSetupRequested";
    public const string PasswordChanged = "Auth.PasswordChanged";
    public const string ProfileUpdated = "User.ProfileUpdated";
    public const string UserSoftDeleted = "Admin.UserSoftDeleted";
    public const string TemporaryPasswordSent = "Admin.TemporaryPasswordSent";
    public const string ConfirmationResent = "Admin.ConfirmationResent";
    public const string SeasonImportStarted = "Import.SeasonStarted";
    public const string SeasonImportCompleted = "Import.SeasonCompleted";
    public const string SeasonImportFailed = "Import.SeasonFailed";
    public const string AllSeasonsImportStarted = "Import.AllSeasonsStarted";
    public const string AllSeasonsImportCompleted = "Import.AllSeasonsCompleted";
    public const string AllSeasonsImportFailed = "Import.AllSeasonsFailed";
    public const string ContactVerificationFailed = "Contact.VerificationFailed";
    public const string ContactMessageSent = "Contact.MessageSent";
    public const string ContactValidationFailed = "Contact.ValidationFailed";
    public const string ContactDeliveryFailed = "Contact.DeliveryFailed";
    public const string GridRankedCompleted = "Game.GridRankedCompleted";
    public const string TeamHistoryRankedCompleted = "Game.TeamHistoryRankedCompleted";
    public const string MissingClubRankedCompleted = "Game.MissingClubRankedCompleted";
    public const string CareerOrderRankedCompleted = "Game.CareerOrderRankedCompleted";
    public const string MysteryPlayerRankedCompleted = "Game.MysteryPlayerRankedCompleted";
    public const string StartingFiveRankedCompleted = "Game.StartingFiveRankedCompleted";
}

public static class AuditResults
{
    public const string Success = "Success";
    public const string Failed = "Failed";
    public const string Denied = "Denied";
    public const string Started = "Started";
}
