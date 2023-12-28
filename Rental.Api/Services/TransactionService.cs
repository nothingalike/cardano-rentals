
using System.Text;
using CardanoSharp.Koios.Client;
using CardanoSharp.Wallet.CIPs.CIP2;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.TransactionBuilding;
using Refit;
using CardanoSharpAsset = CardanoSharp.Wallet.Models.Asset;

public class TransactionService 
{
    private readonly INetworkClient _networkClient;
    private readonly IEpochClient _epochClient;
    private readonly IAddressClient _addressClient;

    public TransactionService()
    {
        _networkClient = RestService.For<INetworkClient>("https://preprod.koios.rest/api/v1");
        _epochClient = RestService.For<IEpochClient>("https://preprod.koios.rest/api/v1");
        _addressClient = RestService.For<IAddressClient>("https://preprod.koios.rest/api/v1");
    }

    public async Task<Transaction?> BuildTransactionForNftSale(Address userAddress, Address providerAddress, CardanoSharpAsset asset)
    {   
        //1. Get UTxOs
        var utxos = await GetUtxos(userAddress.ToString());

        ///2. Create the Body
        var transactionBody = TransactionBodyBuilder.Create;
        
        //set payment outputs
        ITokenBundleBuilder tbb = TokenBundleBuilder.Create
            .AddToken(asset.PolicyId.ToBytes(), asset.Name.ToBytes(), 1);
        transactionBody.AddOutput(providerAddress.GetBytes(), 2000000, tbb);

        //perform coin selection
        var coinSelection = ((TransactionBodyBuilder)transactionBody).UseRandomImprove(utxos, userAddress.ToString());

        //add the inputs from coin selection to transaction body builder
        AddInputsFromCoinSelection(coinSelection, transactionBody);

        //if we have change from coin selection, add to outputs
        if (coinSelection.ChangeOutputs is not null && coinSelection.ChangeOutputs.Any())
        {
            AddChangeOutputs(transactionBody, coinSelection.ChangeOutputs, userAddress.ToString());
        }

        //get protocol parameters and set default fee
        var ppResponse = await _epochClient.GetProtocolParameters();
        var protocolParameters = ppResponse.Content.FirstOrDefault();
        transactionBody.SetFee(protocolParameters.MinFeeB.Value);

        //get network tip and set ttl
        var blockSummaries = (await _networkClient.GetChainTip()).Content;
        var ttl = 2500 + (uint)blockSummaries.First().AbsSlot;
        transactionBody.SetTtl(ttl);

        ///3. Mock Witnesses
        var witnessSet = TransactionWitnessSetBuilder.Create
            .MockVKeyWitness(2);
        
        //metadata
        //consider some type of metadata to help with the renting process

        ///4. Build Draft TX
        //create transaction builder and add the pieces
        var transaction = TransactionBuilder.Create;
        transaction.SetBody(transactionBody);
        transaction.SetWitnesses(witnessSet);
        //transaction.SetAuxData(auxData);

        //get a draft transaction to calculate fee
        var draft = transaction.Build();
        var fee = draft.CalculateFee(protocolParameters.MinFeeA, protocolParameters.MinFeeB);

        //update fee and change output
        transactionBody.SetFee(fee);
        transactionBody.RemoveFeeFromChange();
        
        var rawTx = transaction.Build();
        
        //remove mock witness
        var mockWitnesses = rawTx.TransactionWitnessSet.VKeyWitnesses.Where(x => x.IsMock);
        foreach (var mw in mockWitnesses)
            rawTx.TransactionWitnessSet.VKeyWitnesses.Remove(mw);

        return rawTx;
    }

    private async Task<List<Utxo>> GetUtxos(string address)
    {
        try
        {
            var addressBulkRequest = new AddressBulkRequest { Addresses = new List<string> { address } };
            var addressResponse = (await _addressClient.GetAddressInformation(addressBulkRequest));
            var addressInfo = addressResponse.Content;
            var utxos = new List<Utxo>();

            foreach (var ai in addressInfo.SelectMany(x => x.UtxoSets))
            {
                if(ai is null) continue;
                var utxo = new Utxo()
                {
                    TxIndex = ai.TxIndex,
                    TxHash = ai.TxHash,
                    Balance = new Balance()
                    {
                        Lovelaces = ulong.Parse(ai.Value)
                    }
                };

                var assetList = new List<CardanoSharpAsset>();
                foreach (var aa in ai.AssetList)
                {
                    assetList.Add(new CardanoSharpAsset()
                    {
                        Name = aa.AssetName,
                        PolicyId = aa.PolicyId,
                        Quantity = long.Parse(aa.Quantity)
                    });
                }

                utxo.Balance.Assets = assetList;
                utxos.Add(utxo);
            }

            return utxos;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    private void AddInputsFromCoinSelection(CoinSelection coinSelection, ITransactionBodyBuilder transactionBody)
    {
        foreach (var i in coinSelection.Inputs)
        {
            transactionBody.AddInput(i.TransactionId, i.TransactionIndex);
        }
    }

    private void AddChangeOutputs(ITransactionBodyBuilder ttb, List<TransactionOutput> outputs, string address)
    {
        foreach (var output in outputs)
        {
            ITokenBundleBuilder? assetList = null;

            if (output.Value.MultiAsset is not null)
            {
                assetList = TokenBundleBuilder.Create;
                foreach (var ma in output.Value.MultiAsset)
                {
                    foreach (var na in ma.Value.Token)
                    {
                        assetList.AddToken(ma.Key, na.Key, na.Value);
                    }
                }
            }

            ttb.AddOutput(new Address(address), output.Value.Coin, assetList, outputPurpose: OutputPurpose.Change);
        }
    }

    private Dictionary<string, object> GetMetadata(string rarity, int id, string name, string image, string policyId)
    {
        var file = new
        {
            name = $"{name} Icon",
            mediaType = "image/png",
            src = image
        };
        var fileElement = new List<object>() { file };

        var assetElement = new Dictionary<string, object>()
        {
            {
                Encoding.ASCII.GetBytes($"{name} {rarity}").ToStringHex(), 
                new 
                {
                    name = name,
                    image = image,
                    mediaType = "image/png",
                    files = fileElement,
                    serialNum = $"SOD{rarity}{id}",
                    rarity = rarity
                }
            }
        };

        var policyElement = new Dictionary<string, object>()
        {
            {
                policyId, assetElement
            }
        };

        // return new Dictionary<string, object>()
        // {
        //     {
        //         "721", policyElement
        //     }
        // };
        return policyElement;
    }
}