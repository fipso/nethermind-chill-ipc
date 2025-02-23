// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.State.Healing;

/// <summary>
/// Trie store that can recover from network using eth63-eth66 protocol and GetNodeData.
/// </summary>
public sealed class HealingTrieStore : TrieStore
{
    private ITrieNodeRecovery<IReadOnlyList<Hash256>>? _recovery;

    public HealingTrieStore(
        INodeStorage nodeStorage,
        IPruningStrategy? pruningStrategy,
        IPersistenceStrategy? persistenceStrategy,
        ILogManager? logManager)
        : base(nodeStorage, pruningStrategy, persistenceStrategy, logManager)
    {
    }

    public void InitializeNetwork(ITrieNodeRecovery<IReadOnlyList<Hash256>> recovery)
    {
        _recovery = recovery;
    }

    public override byte[]? LoadRlp(Hash256? address, in TreePath path, Hash256 keccak, ReadFlags readFlags = ReadFlags.None)
    {
        try
        {
            return base.LoadRlp(address, path, keccak, readFlags);
        }
        catch (TrieNodeException)
        {
            if (TryRecover(address, path, keccak, out byte[] rlp))
            {
                return rlp;
            }

            throw;
        }
    }

    private bool TryRecover(Hash256? address, in TreePath path, Hash256 rlpHash, [NotNullWhen(true)] out byte[]? rlp)
    {
        if (_recovery?.CanRecover == true)
        {
            using ArrayPoolList<Hash256> request = new(1) { rlpHash };
            rlp = _recovery.Recover(rlpHash, request).GetAwaiter().GetResult();
            if (rlp is not null)
            {
                _nodeStorage.Set(address, path, rlpHash, rlp);
                return true;
            }
        }

        rlp = null;
        return false;
    }
}
