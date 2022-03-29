using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

//*******************
// MessageQueue
//     revision: 0.1	21.10.21 The Old Goat
//     revision: 0.2    18.03.22 Still The Old Goat
//
// MessageQueue is a self-purging, thread-safe, unbounded
// and interrogable queue for message strings providing
// Monitor semantics via Enqueue/Dequeue methods.
//
// MessageQueue timestamps messages as they are received.
//
// Public Properties:
//    long Now    // value of internal time stamp clock
//    bool Show   // monitor msgs in/out of queue
// Public Methods:
//    void Enqueue(string msg, int author)
//    string Dequeue(ref long timestamp, int iAm, ref bool bailOut))
//
static class MessageQueue {
    private static Stopwatch timestampClock;
    public static long Now {
	get => timestampClock.ElapsedMilliseconds;
    }

    public static bool Show { get; set; }

    // a message container class... wraps a msg with timestamp
    // and "author." Nothing special, really.
    //
    private class EnqueuableMsg {
	public EnqueuableMsg(string msg, int author) {
	    TimeStamp = Now;
	    Text = $"[{author}]: {msg}";
	    Author = author;
	}
	
	public long TimeStamp { get; }
	public string Text { get; }
	public int Author { get; }
    }
    private static List<EnqueuableMsg> messages;

    // Purge messages from the queue with timestamps older
    // than [Enqueued more than]) 5 seconds previously.
    //
    // This method is invoked from an asynchronous task 
    // started in the class static constructor.
    //
    private static void Maintenance() {
	lock (messages) {
	    long ts = Now - 5000;

	    int removeCount = 0;
	    for (; removeCount < messages.Count &&
		     messages[removeCount].TimeStamp < ts; removeCount++);

	    if (removeCount > 0) {
		messages.RemoveRange (0, removeCount);
		if (Show)
		    Console.WriteLine("Maintenance... {0}", messages.Count);
	    }
	}
    }

    // "static" constructor... does a lot of dynamic things,
    // including launching a thread to "maintain" the queue.
    //
    static MessageQueue() {
	messages = new();
	timestampClock = Stopwatch.StartNew();

	Task.Run(async () => {
	    while (true) {
		await Task.Delay(5000);
		Maintenance();
	    }
	});
    }

    // enqueue the received message, along with the current
    // timestamp and the author.
    //
    public static void Enqueue(string msg, int iAm) {
	lock (messages) {
	    messages.Add (new EnqueuableMsg(msg, iAm));
	    Monitor.PulseAll(messages);
	}
	if (Show)
	    Console.WriteLine("<{0}> {1}", iAm, msg);
    }

    // return the text of the oldest message in the queue
    // meeting these criteria:
    //     message timestamp is greater (more recent) than ts
    //     message author != iAm
    // If no such message exists, Wait for an Enqueue. The
    // Wait will be abandoned if bailOut becomes set (true).
    //
    // The timestamp of the returned message is written
    // back to update the ref parameter ts.
    //
    // Note that the message remains in the queue until
    // expressly removed during Maintenance (see below).
    //
    public static string? Dequeue(ref long ts, int iAm, in bool bailOut) {
	lock (messages) {
	    bool timedOut = false;
	    while(! bailOut) {
		if (! timedOut) {
		    foreach (var msg in messages)
			if (msg.TimeStamp > ts && msg.Author != iAm) {
			    ts = msg.TimeStamp;
			    return msg.Text;
			}
		}
		timedOut = ! Monitor.Wait(messages, 500);
	    }
	    return null;
	}
    }
}
