using Renci.SshNet;

SshClient SshClient = null;
var SshPassword = "";
var Args = Environment.GetCommandLineArgs().Skip(1).ToArray();
if (Args.Length > 0)
{
    foreach (var Item in Args)
    {
        ReadFileCommand(Item);
    }
}
else
{
    StartLoop();
}
Console.WriteLine("end runner");

void StartLoop()
{
    var Mode = EnterModeType.File;

    var IsLoop = true;
    while (IsLoop)
    {
        var EnterTypeText = Mode == EnterModeType.File ? "filename" : "";
        Console.Write($"enter command {EnterTypeText}:");

        var UserInput = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(UserInput))
            continue;

        if (UserInput.Contains(' '))
        {
            var InputArray = UserInput.Split(' ');
            var CommandHead = InputArray[0];
            switch (CommandHead.ToLower())
            {
                case "mode":
                    var ModeType = InputArray[1];
                    switch (ModeType.ToLower())
                    {
                        case "file":
                            Mode = EnterModeType.File;
                            Console.WriteLine("mode change file");
                            break;
                        case "user":
                            Mode = EnterModeType.User;
                            Console.WriteLine("mode change user enter");
                            break;
                    }
                    continue;
            }
        }

        switch (UserInput.ToLower())
        {
            case "exit":
                IsLoop = false;
                break;
            default:
                if (Mode == EnterModeType.File)
                    ReadFileCommand(UserInput);
                else if (Mode == EnterModeType.User)
                    RunCommand(UserInput);
                break;
        }
    }
}
void ReadFileCommand(string FileName)
{
    var AllLine = File.ReadAllLines(FileName);
    foreach (var Command in AllLine)
    {
        if (string.IsNullOrWhiteSpace(Command))
            continue;

        AnalyzeAndRunCommand(Command);
    }
}
void RunCommand(string Command)
{
    AnalyzeAndRunCommand(Command);
}

void Ssh_SendCommand(string Command)
{
    if (SshClient is null)
    {
        Console.WriteLine("ssh client is null");
        return;
    }

    if (Command.Contains("sudo"))
        Command = SudoCommand(Command);

    using var CommandResult = SshClient.RunCommand(Command);

    if (!string.IsNullOrWhiteSpace(CommandResult.Result))
        Console.WriteLine(CommandResult.Result);

    if (!string.IsNullOrWhiteSpace(CommandResult.Error))
        Console.WriteLine(CommandResult.Error);
}

string SudoCommand(string Command)
{
    var CommandBody = Command.Replace("sudo", "");
    var NewCommnad = $"echo '{SshPassword}' | sudo -S {CommandBody}";
    return NewCommnad;
}

void AnalyzeAndRunCommand(string Command)
{
    var CommandArray = Command.Split(' ');
    var CommandHead = CommandArray[0];

    Console.WriteLine($"run command:「{Command}」\n");
    switch (CommandHead.ToLower())
    {
        case "ssh":
            SshClient?.Disconnect();
            SshClient?.Dispose();
            SshClient = NewSsh(CommandArray);
            if (SshClient.IsConnected)
                Console.WriteLine($"SSH Client is connected");
            else
                Console.WriteLine($"SSH Client connect error");
            break;
        case "scp":
            ScpUpload(CommandArray);
            break;
        default:
            Ssh_SendCommand(Command);
            break;
    }
}

SshClient NewSsh(string[] CommandArray)
{
    var SshBody = CommandArray[1];
    var SshBodyArray = SshBody.Split('@');
    var SshUserName = SshBodyArray[0];
    var SshHost = SshBodyArray[1];
    SshPassword = CommandArray[2];
    var SshClient = new SshClient(SshHost, SshUserName, SshPassword);
    SshClient.Connect();

    return SshClient;
}

void ScpUpload(string[] CommandArray)
{
    var LocalFileName = CommandArray[1];
    var ScpBody = CommandArray[2];
    var ScpPassword = CommandArray[3];
    var ScpBodyArray = ScpBody.Split('@');
    var ScpUserName = ScpBodyArray[0];
    var RemoteInfo = ScpBodyArray[1];

    var InfoArray = RemoteInfo.Split(':');
    var ScpHost = InfoArray[0];
    var RemotePath = InfoArray[1];

    var ScpClient = new SftpClient(ScpHost, ScpUserName, ScpPassword);
    ScpClient.Connect();
    var Buffer = File.ReadAllBytes(LocalFileName);
    var Info = new FileInfo(LocalFileName);

    var TotalLength = Buffer.Length;
    var Ms = new MemoryStream(Buffer);
    var LastPersent = 0.0;

    Console.WriteLine($"scp upload file {Info.Name} start");

    ScpClient.UploadFile(Ms, RemotePath, (WriteLength) =>
    {
        var Persnet = Math.Floor((double)WriteLength / TotalLength * 100);
        if (Persnet - LastPersent >= 5)
        {
            LastPersent = Persnet;
            Console.WriteLine($"scp upload file {Info.Name}....{Persnet}%");
        }
    });

    Console.WriteLine($"scp upload file {Info.Name} finish");

}

enum CommandForType
{
    None,
    Ssh,
    Scp
}

enum EnterModeType
{
    File,
    User,
}