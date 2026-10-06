namespace HeThongDatTiecCuoi_API.Constants;

public static class AuditActions
{
    public const string CreateAccount = "CREATE_ACCOUNT";
    public const string UpdateAccount = "UPDATE_ACCOUNT";
    public const string LockAccount = "LOCK_ACCOUNT";
    public const string UnlockAccount = "UNLOCK_ACCOUNT";
    public const string ChangeRole = "CHANGE_ROLE";
    public const string ResetPassword = "RESET_PASSWORD";

    public static readonly string[] AccountActions =
    [
        CreateAccount,
        UpdateAccount,
        LockAccount,
        UnlockAccount,
        ChangeRole,
        ResetPassword
    ];
}

public static class AuditEntityNames
{
    public const string User = "User";
}
