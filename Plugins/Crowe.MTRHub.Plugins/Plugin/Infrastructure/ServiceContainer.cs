using Crowe.MTRHub.Plugins.CRUD;

namespace Crowe.MTRHub.Plugins.Infrastructure
{
    public class ServiceContainer
    {
        private readonly ServiceConnection _cnx;
        private RetrieveOperations _retrieve;

        public ServiceContainer(ServiceConnection cnx)
        {
            _cnx = cnx;
        }

        public RetrieveOperations Retrieve =>
            _retrieve ?? (_retrieve = new RetrieveOperations(_cnx));
    }
}
