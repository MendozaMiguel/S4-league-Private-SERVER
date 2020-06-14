namespace Auth.ServiceModel
{
    public enum RegisterResult : byte
    {
        OK,
        AlreadyExists
    }
    public enum LoginResultCode : byte
    {
        OK,
        FAILED,
    }
}
