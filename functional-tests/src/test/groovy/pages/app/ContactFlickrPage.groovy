package pages.app

import org.openqa.selenium.By

import geb.Page

class ContactFlickrPage extends Page {

	static at = { driver.findElement(By.xpath("//*[@id='main-content']/div[1]/div[2]/div[3]/h4")).displayed == true }
	static url = "/connect#flickr"
}
