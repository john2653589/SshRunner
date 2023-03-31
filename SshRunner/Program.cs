using Renci.SshNet;

SshClient SshClient = null;
var SshPassword = "";
var Args = Environment.GetCommandLineArgs().Skip(1).ToArray();

var ArgsDic = new Dictionary<string, string>() { };

if (Args.Length > 0)
{
    var CommandType = "-f";
    foreach (var Item in Args)
    {
        switch (Item.ToLower())
        {
            case "-f":
                CommandType = "-f";
                break;
            case "-p":
                CommandType = "-p";
                break;
            default:
                ArgsDic.Add(CommandType, Item);
                break;
        }
    }

    foreach (var FileName in ArgsDic.Where(Item => Item.Key == "-f"))
    {
        ReadFileCommand(FileName.Value);
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
    var AllParam = ArgsDic
        .Where(Item => Item.Key == "-p")
        .Select(Item => Item.Value);

    foreach (var Command in AllLine)
    {
        if (string.IsNullOrWhiteSpace(Command))
            continue;

        var RunCommand = Command;
        if (AllParam.Any())
        {
            foreach (var Param in AllParam)
            {
                var ParamArray = Param.Trim().Split('=');
                var ParamKey = ParamArray[0];
                var ParamValue = ParamArray[1];
                RunCommand = Command.Replace(ParamKey, ParamValue);
            }
        }
        //Console.WriteLine(RunCommand);
        AnalyzeAndRunCommand(RunCommand);
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

    Console.BackgroundColor = ConsoleColor.Green;
    Console.WriteLine($"run command:「{Command}」\n");
    Console.ResetColor();

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
            ScpSend(CommandArray);
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

void ScpSend(string[] CommandArray)
{
    var ScpPassword = CommandArray.LastOrDefault();

    var LocalFileName = "";
    var ScpBody = "";

    var IsUpload = false;
    foreach (var Item in CommandArray.Skip(1))
    {
        if (Item.Contains('@'))
        {
            ScpBody = Item;
            IsUpload = true;
        }
        else
        {
            LocalFileName = Item;
            IsUpload = false;
        }

        if (!string.IsNullOrWhiteSpace(LocalFileName) && !string.IsNullOrWhiteSpace(ScpBody))
            break;
    }

    var ScpBodyArray = ScpBody.Split('@');
    var ScpUserName = ScpBodyArray[0];
    var RemoteInfo = ScpBodyArray[1];

    var InfoArray = RemoteInfo.Split(':');
    var ScpHost = InfoArray[0];
    var RemotePath = InfoArray[1];

    var ScpClient = new SftpClient(ScpHost, ScpUserName, ScpPassword);
    ScpClient.Connect();

    if (IsUpload)
    {
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
    else
    {
        var Info = new FileInfo(LocalFileName);
        if (Info.Exists)
            Info.Delete();
        using var WriteMs = Info.Create();
        Console.WriteLine($"scp download {Info.Name} start");
        ScpClient.DownloadFile(RemotePath, WriteMs, (WriteLength) =>
        {

        });

        Console.WriteLine($"scp download file {Info.Name} finish");
    }
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