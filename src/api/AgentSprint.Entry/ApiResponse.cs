namespace AgentSprint.Entry;

public sealed record ApiResponse<T>(int Code, T? Data, string Message)
{
    public static ApiResponse<T> Ok(T data)
    {
        return new ApiResponse<T>(0, data, "ok");
    }

    public static ApiResponse<T> Error(string message, int code = 500)
    {
        return new ApiResponse<T>(code, default, message);
    }
}

