namespace NUWM.Servers.Core.Sched
{
    public partial class SubjectInstance : BaseSubject
    {
        private void NullableAll()
        {
            Classroom =
                Lecturer =
                    Streams =
                        SubGroup =
                            Subject =
                                Type =
                                    TimeStamp = "";
        }
        public SubjectInstance()
        {
            NullableAll();
        }

        public SubjectInstance(string dateTime)
        {
            NullableAll();
            TimeStamp = dateTime;
        }
        public SubjectInstance(string dateTime, string subject, string num)
        {
            NullableAll();
            TimeStamp = dateTime;
            Subject = subject;
            LessonNum = int.Parse(num);
        }
    }
}