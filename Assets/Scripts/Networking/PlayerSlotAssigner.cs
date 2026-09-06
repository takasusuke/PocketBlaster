using System.Collections.Generic;

namespace PocketBlaster.Networking
{
    /// <summary>
    /// マルチプレイヤーモード(2026-09-07、オーナー承認: 画面共有・移動オート・各自
    /// レティクル案)で、接続順にスロット(0/1、色分けに使う)を割り当てる純粋ロジック。
    /// UnityEngine非依存にして、EditModeテストから直接検証できるようにしてある
    /// (AmmoState等と同じ狙い)。`MultiplayerStageDirector`が
    /// `PhoneControllerServer.OnPlayerConnected`/`OnPlayerDisconnected`を購読して
    /// これを呼び、割り当て結果に応じて`MultiplayerAimController`を生成/破棄する。
    /// </summary>
    public class PlayerSlotAssigner
    {
        private readonly int _capacity;
        private readonly Dictionary<int, int> _connectionIdToSlot = new Dictionary<int, int>();
        private readonly bool[] _slotOccupied;

        public PlayerSlotAssigner(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 1;
            _slotOccupied = new bool[_capacity];
        }

        /// <summary>
        /// 空きスロットを割り当てる。同じconnectionIdへ重複して呼んでも既存の
        /// 割り当てをそのまま返す(二重登録しない)。満員ならnull。
        /// </summary>
        public int? Assign(int connectionId)
        {
            if (_connectionIdToSlot.TryGetValue(connectionId, out var existing)) return existing;

            for (var slot = 0; slot < _capacity; slot++)
            {
                if (_slotOccupied[slot]) continue;
                _slotOccupied[slot] = true;
                _connectionIdToSlot[connectionId] = slot;
                return slot;
            }
            return null;
        }

        /// <summary>割り当てが無い接続に対しては何もしない。</summary>
        public void Release(int connectionId)
        {
            if (!_connectionIdToSlot.TryGetValue(connectionId, out var slot)) return;
            _slotOccupied[slot] = false;
            _connectionIdToSlot.Remove(connectionId);
        }

        public bool TryGetSlot(int connectionId, out int slot) => _connectionIdToSlot.TryGetValue(connectionId, out slot);
    }
}
