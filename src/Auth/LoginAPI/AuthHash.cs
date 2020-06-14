using System.Security.Cryptography;
using System.Text;

class AuthHash
{
    public static string GetHash256(string data)
    {
        string hashResult = string.Empty;

        if (data != null)
        {
            using (SHA256 sha256 = SHA256Managed.Create())
            {
                byte[] dataBuffer = Encoding.UTF8.GetBytes(data);
                byte[] dataBufferHashed = sha256.ComputeHash(dataBuffer);
                hashResult = GetHashString(dataBufferHashed);
            }
        }
        return hashResult;
    }

    private static string GetHashString(byte[] dataBufferHashed)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in dataBufferHashed)
        {
            sb.Append(b.ToString("X2"));
        }
        return sb.ToString();
    }
}
