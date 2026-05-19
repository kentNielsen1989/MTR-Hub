using Crowe.MTRHub.Plugins.CRUD;

namespace Crowe.MTRHub.Plugins.Infrastructure
{
    public class ServiceContainer
    {
        private readonly ServiceConnection _cnx;
        private RetrieveOperations _retrieve;
        private PORetrieveOperations _poRetrieve;

        public ServiceContainer(ServiceConnection cnx)
        {
            _cnx = cnx;
        }

        public RetrieveOperations Retrieve =>
            _retrieve ?? (_retrieve = new RetrieveOperations(_cnx));

        public PORetrieveOperations PORetrieve =>
            _poRetrieve ?? (_poRetrieve = new PORetrieveOperations(_cnx));
    }
}
