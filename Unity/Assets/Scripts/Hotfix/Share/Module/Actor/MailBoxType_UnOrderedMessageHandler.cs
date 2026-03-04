namespace ET
{
    [Invoke((long)MailBoxType.UnOrderedMessage)]
    public class MailBoxType_UnOrderedMessageHandler: AInvokeHandler<MailBoxInvoker>
    {
        public override void Handle(MailBoxInvoker args)
        {
            HandleAsync(args).Coroutine();
        }
        
        private static async ETTask HandleAsync(MailBoxInvoker args)
        {
            MailBoxComponent mailBoxComponent = args.MailBoxComponent;
            
            MessageObject messageObject = args.MessageObject;
            //LogMsg.Instance.Debug(args.MailBoxComponent.Fiber(), $"MailBoxType_UnOrderedMessageHandler {args.ToString()}");
            await MessageDispatcher.Instance.Handle(mailBoxComponent.Parent, args.FromAddress, messageObject);
        }
    }
}