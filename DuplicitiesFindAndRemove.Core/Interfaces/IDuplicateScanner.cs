using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicitiesFindAndRemove.Core.Interfaces;

public interface IDuplicateScanner
{
    Task<DuplicateDetectionResult> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken = default);
}
