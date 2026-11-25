using Aspire.Hosting;

namespace FinanceTracker.AppHost.Ollama;

/// <summary>
/// Which GPU Ollama's container gets, found on this machine or set with <c>Ollama:Gpu</c>. NVIDIA works on Linux and on
/// Windows through Docker Desktop. AMD only works on Linux, Docker Desktop has no ROCm device on Windows, so there and
/// on a Mac the container runs on the CPU. Those machines are better off with Ollama installed, see
/// <c>Ollama:Endpoint</c> in <see cref="OllamaExtensions"/>.
/// </summary>
internal static class OllamaGpu
{
    /// <summary>
    /// The GPU to pass through, null to run on the CPU. <paramref name="setting"/> wins when it's <c>none</c>,
    /// <c>nvidia</c> or <c>amd</c>, otherwise an NVIDIA card is looked for, then an AMD one.
    /// </summary>
    /// <param name="setting">The <c>Ollama:Gpu</c> setting, null or <c>auto</c> to look.</param>
    public static OllamaGpuVendor? Choose(string? setting)
    {
        string choice = setting?.Trim().ToUpperInvariant() ?? "AUTO";
        if (choice == "NONE")
        {
            return null;
        }

        if (choice == "NVIDIA" || (choice == "AUTO" && HasNvidia()))
        {
            return OllamaGpuVendor.Nvidia;
        }

        if (choice == "AMD" || (choice == "AUTO" && HasAmd()))
        {
            return OllamaGpuVendor.AMD;
        }

        return null;
    }

    /// <summary>
    /// Whether this machine has an NVIDIA card. On Linux its driver's device, on Windows its tool, which the driver puts
    /// in the system folder whether or not it's on the PATH. Docker also needs the NVIDIA Container Toolkit on Linux,
    /// or Docker Desktop's WSL 2 engine on Windows, set <c>Ollama:Gpu</c> to <c>none</c> without it.
    /// </summary>
    private static bool HasNvidia() =>
        File.Exists("/dev/nvidia0")
        || File.Exists(Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"))
        || IsOnPath("nvidia-smi");

    /// <summary>
    /// Whether this machine has an AMD card ROCm can use, the compute device it makes on Linux. Never on Windows or a
    /// Mac, where Docker can't pass one through.
    /// </summary>
    private static bool HasAmd() => OperatingSystem.IsLinux() && File.Exists("/dev/kfd");

    /// <summary>
    /// Whether <paramref name="tool"/> is on the PATH, with or without Windows' <c>.exe</c>.
    /// </summary>
    /// <param name="tool">The tool's name.</param>
    private static bool IsOnPath(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Any(folder => File.Exists(Path.Combine(folder, tool)) || File.Exists(Path.Combine(folder, $"{tool}.exe")));
}
