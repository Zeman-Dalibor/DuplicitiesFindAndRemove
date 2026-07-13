using DuplicitiesFindAndRemove.Core.Volume;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IVolumePathResolver
{
    VolumePathInfo Resolve(string filePath);
}
