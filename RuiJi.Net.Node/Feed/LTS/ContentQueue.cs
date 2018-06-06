﻿using Amib.Threading;
using RuiJi.Net.Core.Extracter;
using RuiJi.Net.Core.Queue;
using RuiJi.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RuiJi.Net.NodeVisitor;
using RuiJi.Net.Core;
using System.IO;
using Newtonsoft.Json;
using RuiJi.Net.Core.Utils;

namespace RuiJi.Net.Node.Feed.LTS
{
    public class QueueModel
    {
        public string Url { get; set; }

        public int FeedId { get; set; }
    }

    public class ContentQueue
    {
        private static ContentQueue contentQueue;

        private MessageQueue<QueueModel> queue;
        private SmartThreadPool pool;
        private STPStartInfo stpStartInfo;
        private IStorage<ContentModel> storage;
        private string path;

        static ContentQueue()
        {
            contentQueue = new ContentQueue();
        }

        private ContentQueue()
        {
            path = AppDomain.CurrentDomain.BaseDirectory + "save_failed";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            storage = new FeedProxyStorage();

            queue = new MessageQueue<QueueModel>();
            queue.ContentChanged += queue_ContentChanged;

            stpStartInfo = new STPStartInfo
            {
                IdleTimeout = 3000,
                MaxWorkerThreads = 8,
                MinWorkerThreads = 0
            };

            pool = new SmartThreadPool(stpStartInfo);
        }

        public static ContentQueue Instance
        {
            get
            {
                return contentQueue;
            }
        }

        private void queue_ContentChanged(object sender, QueueChangedEventArgs<QueueModel> args)
        {
            if (args.Action == QueueChangedActionEnum.Enqueue)
            {
                pool.QueueWorkItem(() =>
                {
                    try
                    {
                        QueueModel qm;
                        if (queue.TryDequeue(out qm))
                        {
                            var visitor = new Visitor();

                            var results = visitor.Extract(qm.Url);
                            if (results.Count > 0)
                            {
                                var result = results.OrderByDescending(m => m.Metas.Count).First();
                                var cm = new ContentModel();
                                cm.FeedId = qm.FeedId;
                                cm.Url = qm.Url;
                                cm.Metas = result.Metas;

                                if (!storage.Save(cm))
                                    File.AppendAllText(path + @"\" + EncryptHelper.GetMD5Hash(qm.Url) + ".json", JsonConvert.SerializeObject(cm));
                            }
                        }
                    }
                    catch { }
                });
            }
        }

        private void Save(List<ExtractResult> articles)
        {
            throw new NotImplementedException();
        }

        internal void Enqueue(QueueModel v)
        {
            queue.Enqueue(v);
        }
    }
}