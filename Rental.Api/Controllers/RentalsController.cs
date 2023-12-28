using CardanoSharp.Koios.Client;
using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Refit;

namespace Rental.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class RentalsController : ControllerBase
{
    private readonly string _providerMnemonic;
    private readonly string _userMnemonic;
    private readonly IAccountClient _accountClient;
    private readonly IAddressClient _addressClient;

    private readonly ILogger<RentalsController> _logger;

    public RentalsController(ILogger<RentalsController> logger)
    {
        _logger = logger;
        _userMnemonic = "whisper find flash upgrade ask great tank nerve salute sadness barrel casino kiwi sugar merit tooth frozen ship neutral puzzle chimney mixture axis margin";
        _providerMnemonic = "blush seat chunk tackle steel magic dwarf know okay leaf guess great cousin salute best skirt tuna security cross gain slide pear half hill";
        _accountClient = RestService.For<IAccountClient>("https://preprod.koios.rest/api/v1");
        _addressClient = RestService.For<IAddressClient>("https://preprod.koios.rest/api/v1");
    }

    [HttpGet("CheckProviderBalance")]
    public async Task<IActionResult> CheckProviderBalanceAsync()
    {
        var providerAccountNode = new MnemonicService().Restore(_providerMnemonic)
            .GetMasterNode()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        var stakeNode = providerAccountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        var providerAddress = AddressUtility.GetStakeAddress(stakeNode.PublicKey, NetworkType.Preprod);

        var accountInformation = await _accountClient.GetAccountInformation(new AccountBulkRequest() { StakeAddresses = [ providerAddress.ToString() ] });

        if(accountInformation.Error != null)
            return BadRequest(accountInformation.Error.Message);
        
        if(accountInformation.Content.Length == 0)
            return BadRequest("Provider has no balance");
        
        return Ok(new { TotalBalanse = float.Parse(accountInformation.Content[0].TotalBalance) / 1000000 });
    }

    [HttpGet("UserAssets")]
    public async Task<IActionResult> GetUserAssetsAsync() 
    {
        var userAccountNode = new MnemonicService().Restore(_userMnemonic)
            .GetMasterNode()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        var paymentNode = userAccountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);

        var stakeNode = userAccountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        var providerAddress = AddressUtility.GetBaseAddress(paymentNode.PublicKey, stakeNode.PublicKey, NetworkType.Preprod);

        var addressAssets = await _addressClient.GetAddressAssets(new AddressBulkRequest() { Addresses = [ providerAddress.ToString() ] });

        return Ok(addressAssets.Content);
    }

    [HttpGet("ProviderAssets")]
    public async Task<IActionResult> GetProviderAssetsAsync() 
    {
        var providerAccountNode = new MnemonicService().Restore(_providerMnemonic)
            .GetMasterNode()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        var paymentNode = providerAccountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);

        var stakeNode = providerAccountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        var providerAddress = AddressUtility.GetBaseAddress(paymentNode.PublicKey, stakeNode.PublicKey, NetworkType.Preprod);

        var addressAssets = await _addressClient.GetAddressAssets(new AddressBulkRequest() { Addresses = [ providerAddress.ToString() ] });

        return Ok(addressAssets.Content);
    }

    [HttpGet("CheckUserBalance")]
    public async Task<IActionResult> CheckUserBalanceAsync()
    {
        var userAccountNode = new MnemonicService().Restore(_userMnemonic)
            .GetMasterNode()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        var stakeNode = userAccountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        var userAddress = AddressUtility.GetStakeAddress(stakeNode.PublicKey, NetworkType.Preprod);

        var accountInformation = await _accountClient.GetAccountInformation(new AccountBulkRequest() { StakeAddresses = [ userAddress.ToString() ] });

        if(accountInformation.Error != null)
            return BadRequest(accountInformation.Error.Message);
        
        if(accountInformation.Content.Length == 0)
            return BadRequest("User has no balance");
        
        return Ok(new { TotalBalanse = float.Parse(accountInformation.Content[0].TotalBalance) / 1000000 });
    }

    [HttpPost("Lock")]
    public async Task<IActionResult> LockAsync()
    {
        var userAccountNode = new MnemonicService().Restore(_userMnemonic)
            .GetMasterNode()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        var userPaymentNode = userAccountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);

        var userStakeNode = userAccountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        
        var providerAccountNode = new MnemonicService().Restore(_providerMnemonic)
            .GetMasterNode()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        var providerPaymentNode = providerAccountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);

        var providerStakeNode = providerAccountNode
            .Derive(RoleType.Staking)
            .Derive(0);
    }

    [HttpPost("Release")]
    public async Task<IActionResult> ReleaseAsync()
    {
        return Ok(new {});
    }
}
