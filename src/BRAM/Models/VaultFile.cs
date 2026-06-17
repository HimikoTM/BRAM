namespace RobloxAccountManager.Models;

public sealed class VaultFile
{
    public int Version { get; set; } = 2;
    public string Mode { get; set; } = "dpapi";

    public KdfInfo? Kdf { get; set; }
    public string? Verifier { get; set; }
    public string? Nonce { get; set; }
    public string? Tag { get; set; }

    public string Cipher { get; set; } = "";
}

public sealed class KdfInfo
{
    public string Algo { get; set; } = "PBKDF2-SHA256";
    public int Iterations { get; set; } = 310_000;
    public string Salt { get; set; } = "";
}
