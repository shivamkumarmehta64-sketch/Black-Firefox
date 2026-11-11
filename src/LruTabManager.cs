using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace BlackBrowser
{
    /// <summary>
    /// Computer Science Concept: LRU (Least Recently Used) Cache with Doubly Linked List + Hash Map
    /// Provides O(1) tab access recording, O(1) eviction of stale background tabs, and automatic memory suspension.
    /// </summary>
    public class LruTabManager
    {
        private class LruNode
        {
            public TabPage Page;
            public WebView2 WebView;
            public bool IsSuspended;
            public DateTime LastAccessed;
            public LruNode Prev;
            public LruNode Next;

            public LruNode(TabPage page, WebView2 wv)
            {
                Page = page;
                WebView = wv;
                IsSuspended = false;
                LastAccessed = DateTime.UtcNow;
            }
        }

        private readonly Dictionary<TabPage, LruNode> _map = new Dictionary<TabPage, LruNode>();
        private LruNode _head; // Most recently used
        private LruNode _tail; // Least recently used
        private readonly int _maxActiveTabs;
        private readonly object _lock = new object();

        public LruTabManager(int maxActiveTabs = 4)
        {
            _maxActiveTabs = Math.Max(2, maxActiveTabs);
        }

        public void RegisterTab(TabPage page, WebView2 wv)
        {
            if (page == null || wv == null) return;
            lock (_lock)
            {
                if (_map.ContainsKey(page))
                {
                    Touch(page);
                    return;
                }

                LruNode node = new LruNode(page, wv);
                _map[page] = node;
                AddFirst(node);
                EnforceCapacity();
            }
        }

        public void Touch(TabPage page)
        {
            if (page == null) return;
            lock (_lock)
            {
                LruNode node;
                if (!_map.TryGetValue(page, out node)) return;

                node.LastAccessed = DateTime.UtcNow;
                if (node.WebView != null && !node.WebView.IsDisposed)
                {
                    try
                    {
                        if (!node.WebView.Visible) node.WebView.Visible = true;
                        if (node.IsSuspended && node.WebView.CoreWebView2 != null)
                        {
                            node.WebView.CoreWebView2.Resume();
                        }
                        node.IsSuspended = false;
                    }
                    catch { }
                }

                MoveToHead(node);
                EnforceCapacity();
            }
        }

        public void RemoveTab(TabPage page)
        {
            if (page == null) return;
            lock (_lock)
            {
                LruNode node;
                if (!_map.TryGetValue(page, out node)) return;

                RemoveNode(node);
                _map.Remove(page);
            }
        }

        public void SuspendAllBackground(TabPage activePage)
        {
            lock (_lock)
            {
                foreach (var kvp in _map)
                {
                    if (kvp.Key != activePage)
                    {
                        SuspendNode(kvp.Value);
                    }
                }
            }
        }

        private void EnforceCapacity()
        {
            int activeCount = 0;
            LruNode curr = _head;
            while (curr != null)
            {
                if (!curr.IsSuspended)
                {
                    activeCount++;
                    if (activeCount > _maxActiveTabs)
                    {
                        SuspendNode(curr);
                    }
                }
                curr = curr.Next;
            }
        }

        private static void SuspendNode(LruNode node)
        {
            if (node == null || node.IsSuspended || node.WebView == null || node.WebView.IsDisposed) return;
            try
            {
                if (node.WebView.CoreWebView2 != null)
                {
                    // Never suspend tabs actively playing audio
                    if (node.WebView.CoreWebView2.IsDocumentPlayingAudio) return;

                    // WebView2 requires the control to be hidden before TrySuspendAsync succeeds
                    if (node.WebView.Visible) node.WebView.Visible = false;

                    var task = node.WebView.CoreWebView2.TrySuspendAsync();
                    var uiScheduler = System.Threading.SynchronizationContext.Current != null
                        ? System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext()
                        : System.Threading.Tasks.TaskScheduler.Default;
                    task.ContinueWith(t =>
                    {
                        try
                        {
                            if (!t.IsFaulted && !t.IsCanceled && t.Result)
                            {
                                node.IsSuspended = true;
                            }
                        }
                        catch { }
                    }, uiScheduler);
                }
            }
            catch { }
        }

        private void AddFirst(LruNode node)
        {
            node.Next = _head;
            node.Prev = null;
            if (_head != null) _head.Prev = node;
            _head = node;
            if (_tail == null) _tail = node;
        }

        private void RemoveNode(LruNode node)
        {
            if (node.Prev != null) node.Prev.Next = node.Next;
            else _head = node.Next;

            if (node.Next != null) node.Next.Prev = node.Prev;
            else _tail = node.Prev;

            node.Prev = null;
            node.Next = null;
        }

        private void MoveToHead(LruNode node)
        {
            if (node == _head) return;
            RemoveNode(node);
            AddFirst(node);
        }
    }
}
