using System;

namespace Beamable.Common.Utils
{
    [Serializable]
    public struct Response<T>
    {
        public T data;
        public string errorMessage;

        public Response(T data, string errorMessage = null)
        {
            this.data = data;
            this.errorMessage = errorMessage;
        }
    }
}