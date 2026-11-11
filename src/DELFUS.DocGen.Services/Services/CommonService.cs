using Dapper;
using DELFUS.Entities;
using DELFUS.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.DocGen.Service
{
    public interface ICommonService : IDisposable
    {
        Task<IEnumerable<ParsingField>> GetParsingSchema(string recordType);
        Task<int> UpdateBatchPipelineStatus(int batchId, string pipelineStatus);
        Task<int> UpdateLienDisqualifyReason(int lienId, string disqualifyReason);
    }

    public class CommonService : ServiceBase, ICommonService
    {
        public async Task<IEnumerable<ParsingField>> GetParsingSchema(string recordType)
        {
            return await db.QueryAsync<ParsingField>("SELECT * FROM [Schema].[Request] WHERE RecordType=@RecordType AND IsActive=1", new { RecordType = recordType });
        }

        public async Task<int> UpdateBatchPipelineStatus(int batchId, string pipelineStatus)
        {
            string query = "UPDATE [dbo].[Batches] SET PipelineStatus = @PipelineStatus, {0}On = @TimeStamp WHERE Id = @BatchId".Fill(pipelineStatus);

            return await db.ExecuteAsync(query, new { PipelineStatus = pipelineStatus, TimeStamp = Common.Now, BatchId = batchId });
        }

        public async Task<int> UpdateLienDisqualifyReason(int lienId, string disqualifyReason)
        {
            string query = "UPDATE [dbo].[LienData] SET DisqualifyReason = @DisqualifyReason, MailPieceStatus=@MailPieceStatus WHERE Id = @LienId";

            return await db.ExecuteAsync(query, new { DisqualifyReason = disqualifyReason, MailPieceStatus = RecordStatus.Disqualified, LienId = lienId });
        }
    }
}
